using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine.Assertions;

namespace Unity.Physics.Systems
{
    // A system which builds the physics world based on the entity world.
    // The world will contain a rigid body for every entity which has a rigid body component,
    // and a joint for every entity which has a joint component.
    public class BuildPhysicsWorld : SystemBase, IPhysicsSystem
    {
        private JobHandle m_InputDependency;
        private JobHandle m_OutputDependency;

        public PhysicsWorld PhysicsWorld = new PhysicsWorld(0, 0, 0);

        [Obsolete("FinalJobHandle has been deprecated. Use GetOutputDependency() instead. (RemovedAfter 2020-08-07) (UnityUpgradable) -> GetOutputDependency()")]
        public JobHandle FinalJobHandle => GetOutputDependency();

        // Entity group queries
        public EntityQuery DynamicEntityGroup { get; private set; }
        public EntityQuery StaticEntityGroup { get; private set; }
        public EntityQuery JointEntityGroup { get; private set; }

        public EntityQuery CollisionWorldProxyGroup { get; private set; }

        StepPhysicsWorld m_StepPhysicsWorldSystem;
        EndFramePhysicsSystem m_EndFramePhysicsSystem;

        protected override void OnCreate()
        {
            base.OnCreate();

            DynamicEntityGroup = GetEntityQuery(new EntityQueryDesc
            {
                All = new ComponentType[]
                {
                    typeof(PhysicsVelocity),
                    typeof(Translation),
                    typeof(Rotation)
                },
                None = new ComponentType[]
                {
                    typeof(PhysicsExclude)
                }
            });

            StaticEntityGroup = GetEntityQuery(new EntityQueryDesc
            {
                All = new ComponentType[]
                {
                    typeof(PhysicsCollider)
                },
                Any = new ComponentType[]
                {
                    typeof(LocalToWorld),
                    typeof(Translation),
                    typeof(Rotation)
                },
                None = new ComponentType[]
                {
                    typeof(PhysicsExclude),
                    typeof(PhysicsVelocity)
                }
            });

            JointEntityGroup = GetEntityQuery(new EntityQueryDesc
            {
                All = new ComponentType[]
                {
                    typeof(PhysicsConstrainedBodyPair),
                    typeof(PhysicsJoint)
                },
                None = new ComponentType[]
                {
                    typeof(PhysicsExclude)
                }
            });

            CollisionWorldProxyGroup = GetEntityQuery(ComponentType.ReadWrite<CollisionWorldProxy>());

            m_StepPhysicsWorldSystem = World.GetOrCreateSystem<StepPhysicsWorld>();
            m_EndFramePhysicsSystem = World.GetOrCreateSystem<EndFramePhysicsSystem>();
        }

        protected override void OnDestroy()
        {
            PhysicsWorld.Dispose();
            base.OnDestroy();
        }

        protected override void OnUpdate()
        {
            // Make sure last frame's physics jobs are complete before any new ones start
            m_EndFramePhysicsSystem.GetOutputDependency().Complete();

            // Combine implicit input dependency with the user one
            Dependency = JobHandle.CombineDependencies(Dependency, m_InputDependency);

            // Extract types used by initialize jobs
            var entityType = GetEntityTypeHandle();
            var localToWorldType = GetComponentTypeHandle<LocalToWorld>(true);
            var parentType = GetComponentTypeHandle<Parent>(true);
            var positionType = GetComponentTypeHandle<Translation>(true);
            var rotationType = GetComponentTypeHandle<Rotation>(true);
            var physicsColliderType = GetComponentTypeHandle<PhysicsCollider>(true);
            var physicsVelocityType = GetComponentTypeHandle<PhysicsVelocity>(true);
            var physicsMassType = GetComponentTypeHandle<PhysicsMass>(true);
            var physicsMassOverrideType = GetComponentTypeHandle<PhysicsMassOverride>(true);
            var physicsDampingType = GetComponentTypeHandle<PhysicsDamping>(true);
            var physicsGravityFactorType = GetComponentTypeHandle<PhysicsGravityFactor>(true);
            var physicsCustomTagsType = GetComponentTypeHandle<PhysicsCustomTags>(true);
            var physicsConstrainedBodyPairType = GetComponentTypeHandle<PhysicsConstrainedBodyPair>(true);
            var physicsJointType = GetComponentTypeHandle<PhysicsJoint>(true);

            int numDynamicBodies = DynamicEntityGroup.CalculateEntityCount();
            int numStaticBodies = StaticEntityGroup.CalculateEntityCount();
            int numJoints = JointEntityGroup.CalculateEntityCount();

            int previousStaticBodyCount = PhysicsWorld.NumStaticBodies;

            // Resize the world's native arrays
            PhysicsWorld.Reset(
                numStaticBodies + 1, // +1 for the default static body
                numDynamicBodies,
                numJoints);

            // Determine if the static bodies have changed in any way that will require the static broadphase tree to be rebuilt
            JobHandle staticBodiesCheckHandle = default;
            var haveStaticBodiesChanged = new NativeArray<int>(1, Allocator.TempJob);
            haveStaticBodiesChanged[0] = 0;
            {
                if (PhysicsWorld.NumStaticBodies != previousStaticBodyCount)
                {
                    haveStaticBodiesChanged[0] = 1;
                }
                else
                {
                    // Make a job to test for changes
                    int numChunks;
                    using (NativeArray<ArchetypeChunk> chunks = StaticEntityGroup.CreateArchetypeChunkArray(Allocator.TempJob))
                    {
                        numChunks = chunks.Length;
                    }
                    var chunksHaveChanges = new NativeArray<int>(numChunks, Allocator.TempJob);

                    staticBodiesCheckHandle = new Jobs.CheckStaticBodyChangesJob
                    {
                        LocalToWorldType = localToWorldType,
                        ParentType = parentType,
                        PositionType = positionType,
                        RotationType = rotationType,
                        PhysicsColliderType = physicsColliderType,
                        ChunkHasChangesOutput = chunksHaveChanges,
                        m_LastSystemVersion = LastSystemVersion
                    }.Schedule(StaticEntityGroup, Dependency);

                    staticBodiesCheckHandle = new Jobs.CheckStaticBodyChangesReduceJob
                    {
                        ChunkHasChangesOutput = chunksHaveChanges,
                        Result = haveStaticBodiesChanged
                    }.Schedule(staticBodiesCheckHandle);
                }
            }

            using (var jobHandles = new NativeList<JobHandle>(4, Allocator.Temp))
            {
                // Static body changes check jobs
                jobHandles.Add(staticBodiesCheckHandle);

                // Create the default static body at the end of the body list
                // TODO: could skip this if no joints present
                jobHandles.Add(new Jobs.CreateDefaultStaticRigidBody
                {
                    NativeBodies = PhysicsWorld.Bodies,
                    BodyIndex = PhysicsWorld.Bodies.Length - 1
                }.Schedule(Dependency));

                // Dynamic bodies.
                // Create these separately from static bodies to maintain a 1:1 mapping
                // between dynamic bodies and their motions.
                if (numDynamicBodies > 0)
                {
                    jobHandles.Add(new Jobs.CreateRigidBodies
                    {
                        EntityType = entityType,
                        LocalToWorldType = localToWorldType,
                        ParentType = parentType,
                        PositionType = positionType,
                        RotationType = rotationType,
                        PhysicsColliderType = physicsColliderType,
                        PhysicsCustomTagsType = physicsCustomTagsType,

                        FirstBodyIndex = 0,
                        RigidBodies = PhysicsWorld.Bodies
                    }.Schedule(DynamicEntityGroup, Dependency));

                    jobHandles.Add(new Jobs.CreateMotions
                    {
                        PositionType = positionType,
                        RotationType = rotationType,
                        PhysicsVelocityType = physicsVelocityType,
                        PhysicsMassType = physicsMassType,
                        PhysicsMassOverrideType = physicsMassOverrideType,
                        PhysicsDampingType = physicsDampingType,
                        PhysicsGravityFactorType = physicsGravityFactorType,

                        MotionDatas = PhysicsWorld.MotionDatas,
                        MotionVelocities = PhysicsWorld.MotionVelocities
                    }.Schedule(DynamicEntityGroup, Dependency));
                }

                // Now, schedule creation of static bodies, with FirstBodyIndex pointing after 
                // the dynamic and kinematic bodies
                if (numStaticBodies > 0)
                {
                    jobHandles.Add(new Jobs.CreateRigidBodies
                    {
                        EntityType = entityType,
                        LocalToWorldType = localToWorldType,
                        ParentType = parentType,
                        PositionType = positionType,
                        RotationType = rotationType,
                        PhysicsColliderType = physicsColliderType,
                        PhysicsCustomTagsType = physicsCustomTagsType,

                        FirstBodyIndex = numDynamicBodies,
                        RigidBodies = PhysicsWorld.Bodies
                    }.Schedule(StaticEntityGroup, Dependency));
                }

                var handle = JobHandle.CombineDependencies(jobHandles);
                jobHandles.Clear();

                // Build joints
                if (numJoints > 0)
                {
                    jobHandles.Add(new Jobs.CreateJoints
                    {
                        ConstrainedBodyPairComponentType = physicsConstrainedBodyPairType,
                        JointComponentType = physicsJointType,
                        EntityType = entityType,
                        RigidBodies = PhysicsWorld.Bodies,
                        Joints = PhysicsWorld.Joints,
                        DefaultStaticBodyIndex = PhysicsWorld.Bodies.Length - 1,
                        NumDynamicBodies = numDynamicBodies
                    }.Schedule(JointEntityGroup, handle));
                }

                // Build the broadphase
                // TODO: could optimize this by gathering the AABBs and filters at the same time as building the bodies above
#if !UNITY_DOTSPLAYER
                float timeStep = UnityEngine.Time.fixedDeltaTime;
#else
                float timeStep = Time.DeltaTime;
#endif

                PhysicsStep stepComponent = PhysicsStep.Default;
                if (HasSingleton<PhysicsStep>())
                {
                    stepComponent = GetSingleton<PhysicsStep>();
                }

                JobHandle buildBroadphaseHandle = PhysicsWorld.CollisionWorld.ScheduleBuildBroadphaseJobs(
                    ref PhysicsWorld, timeStep, stepComponent.Gravity,
                    haveStaticBodiesChanged, handle, stepComponent.ThreadCountHint);
                jobHandles.Add(haveStaticBodiesChanged.Dispose(buildBroadphaseHandle));

                m_OutputDependency = JobHandle.CombineDependencies(jobHandles);
            }

            // Combine implicit output dependency with user one
            Dependency = JobHandle.CombineDependencies(m_OutputDependency, Dependency);

            // Inform next system in the pipeline of its dependency
            m_StepPhysicsWorldSystem.AddInputDependency(Dependency);

            // Invalidate input dependency since it's been used by now
            m_InputDependency = default;
        }

        public void AddInputDependency(JobHandle inputDep)
        {
            m_InputDependency = JobHandle.CombineDependencies(m_InputDependency, inputDep);
        }

        public JobHandle GetOutputDependency()
        {
            return m_OutputDependency;
        }

        #region Jobs

        private static class Jobs
        {
            [BurstCompile]
            internal struct CheckStaticBodyChangesJob : IJobChunk
            {
                [ReadOnly] public ComponentTypeHandle<LocalToWorld> LocalToWorldType;
                [ReadOnly] public ComponentTypeHandle<Parent> ParentType;
                [ReadOnly] public ComponentTypeHandle<Translation> PositionType;
                [ReadOnly] public ComponentTypeHandle<Rotation> RotationType;
                [ReadOnly] public ComponentTypeHandle<PhysicsCollider> PhysicsColliderType;

                public NativeArray<int> ChunkHasChangesOutput;
                public uint m_LastSystemVersion;

                public void Execute(ArchetypeChunk chunk, int chunkIndex, int firstEntityIndex)
                {
                    bool didChunkChange =
                        chunk.DidChange(LocalToWorldType, m_LastSystemVersion) ||
                        chunk.DidChange(PositionType, m_LastSystemVersion) ||
                        chunk.DidChange(RotationType, m_LastSystemVersion) ||
                        chunk.DidChange(PhysicsColliderType, m_LastSystemVersion) ||
                        chunk.DidOrderChange(m_LastSystemVersion);
                    ChunkHasChangesOutput[chunkIndex] = didChunkChange ? 1 : 0;
                }
            }

            [BurstCompile]
            internal struct CheckStaticBodyChangesReduceJob : IJob
            {
                [ReadOnly] [DeallocateOnJobCompletion] public NativeArray<int> ChunkHasChangesOutput;
                public NativeArray<int> Result;

                public void Execute()
                {
                    for (int i = 0; i < ChunkHasChangesOutput.Length; i++)
                    {
                        if (ChunkHasChangesOutput[i] > 0)
                        {
                            Result[0] = 1;
                            return;
                        }
                    }

                    Result[0] = 0;
                }
            }

            [BurstCompile]
            internal struct CreateDefaultStaticRigidBody : IJob
            {
                [NativeDisableContainerSafetyRestriction]
                public NativeArray<RigidBody> NativeBodies;
                public int BodyIndex;

                public void Execute()
                {
                    NativeBodies[BodyIndex] = new RigidBody
                    {
                        WorldFromBody = new RigidTransform(quaternion.identity, float3.zero),
                        Collider = default,
                        Entity = Entity.Null,
                        CustomTags = 0
                    };
                }
            }

            [BurstCompile]
            internal struct CreateRigidBodies : IJobChunk
            {
                [ReadOnly] public EntityTypeHandle EntityType;
                [ReadOnly] public ComponentTypeHandle<LocalToWorld> LocalToWorldType;
                [ReadOnly] public ComponentTypeHandle<Parent> ParentType;
                [ReadOnly] public ComponentTypeHandle<Translation> PositionType;
                [ReadOnly] public ComponentTypeHandle<Rotation> RotationType;
                [ReadOnly] public ComponentTypeHandle<PhysicsCollider> PhysicsColliderType;
                [ReadOnly] public ComponentTypeHandle<PhysicsCustomTags> PhysicsCustomTagsType;
                [ReadOnly] public int FirstBodyIndex;

                [NativeDisableContainerSafetyRestriction] public NativeArray<RigidBody> RigidBodies;

                public void Execute(ArchetypeChunk chunk, int chunkIndex, int firstEntityIndex)
                {
                    NativeArray<PhysicsCollider> chunkColliders = chunk.GetNativeArray(PhysicsColliderType);
                    NativeArray<LocalToWorld> chunkLocalToWorlds = chunk.GetNativeArray(LocalToWorldType);
                    NativeArray<Translation> chunkPositions = chunk.GetNativeArray(PositionType);
                    NativeArray<Rotation> chunkRotations = chunk.GetNativeArray(RotationType);
                    NativeArray<Entity> chunkEntities = chunk.GetNativeArray(EntityType);
                    NativeArray<PhysicsCustomTags> chunkCustomTags = chunk.GetNativeArray(PhysicsCustomTagsType);

                    int instanceCount = chunk.Count;
                    int rbIndex = FirstBodyIndex + firstEntityIndex;

                    bool hasChunkPhysicsColliderType = chunk.Has(PhysicsColliderType);
                    bool hasChunkPhysicsCustomTagsType = chunk.Has(PhysicsCustomTagsType);
                    bool hasChunkParentType = chunk.Has(ParentType);
                    bool hasChunkLocalToWorldType = chunk.Has(LocalToWorldType);
                    bool hasChunkPositionType = chunk.Has(PositionType);
                    bool hasChunkRotationType = chunk.Has(RotationType);

                    RigidTransform worldFromBody = RigidTransform.identity;
                    for (int i = 0; i < instanceCount; i++, rbIndex++)
                    {
                        // if entities are in a transform hierarchy then Translation/Rotation are in the space of their parents
                        // in that case, LocalToWorld is the only common denominator for world space
                        if (hasChunkParentType)
                        {
                            if (hasChunkLocalToWorldType)
                            {
                                var localToWorld = chunkLocalToWorlds[i];
                                worldFromBody = Math.DecomposeRigidBodyTransform(localToWorld.Value);
                            }
                        }
                        else
                        {
                            if (hasChunkPositionType)
                            {
                                worldFromBody.pos = chunkPositions[i].Value;
                            }
                            else if (hasChunkLocalToWorldType)
                            {
                                worldFromBody.pos = chunkLocalToWorlds[i].Position;
                            }

                            if (hasChunkRotationType)
                            {
                                worldFromBody.rot = chunkRotations[i].Value;
                            }
                            else if (hasChunkLocalToWorldType)
                            {
                                var localToWorld = chunkLocalToWorlds[i];
                                worldFromBody.rot = Math.DecomposeRigidBodyOrientation(localToWorld.Value);
                            }
                        }

                        RigidBodies[rbIndex] = new RigidBody
                        {
                            WorldFromBody = new RigidTransform(worldFromBody.rot, worldFromBody.pos),
                            Collider = hasChunkPhysicsColliderType ? chunkColliders[i].Value : default,
                            Entity = chunkEntities[i],
                            CustomTags = hasChunkPhysicsCustomTagsType ? chunkCustomTags[i].Value : (byte)0
                        };
                    }
                }
            }

            [BurstCompile]
            internal struct CreateMotions : IJobChunk
            {
                [ReadOnly] public ComponentTypeHandle<Translation> PositionType;
                [ReadOnly] public ComponentTypeHandle<Rotation> RotationType;
                [ReadOnly] public ComponentTypeHandle<PhysicsVelocity> PhysicsVelocityType;
                [ReadOnly] public ComponentTypeHandle<PhysicsMass> PhysicsMassType;
                [ReadOnly] public ComponentTypeHandle<PhysicsMassOverride> PhysicsMassOverrideType;
                [ReadOnly] public ComponentTypeHandle<PhysicsDamping> PhysicsDampingType;
                [ReadOnly] public ComponentTypeHandle<PhysicsGravityFactor> PhysicsGravityFactorType;

                [NativeDisableParallelForRestriction] public NativeArray<MotionData> MotionDatas;
                [NativeDisableParallelForRestriction] public NativeArray<MotionVelocity> MotionVelocities;

                public void Execute(ArchetypeChunk chunk, int chunkIndex, int firstEntityIndex)
                {
                    NativeArray<Translation> chunkPositions = chunk.GetNativeArray(PositionType);
                    NativeArray<Rotation> chunkRotations = chunk.GetNativeArray(RotationType);
                    NativeArray<PhysicsVelocity> chunkVelocities = chunk.GetNativeArray(PhysicsVelocityType);
                    NativeArray<PhysicsMass> chunkMasses = chunk.GetNativeArray(PhysicsMassType);
                    NativeArray<PhysicsMassOverride> chunkMassOverrides = chunk.GetNativeArray(PhysicsMassOverrideType);
                    NativeArray<PhysicsDamping> chunkDampings = chunk.GetNativeArray(PhysicsDampingType);
                    NativeArray<PhysicsGravityFactor> chunkGravityFactors = chunk.GetNativeArray(PhysicsGravityFactorType);

                    int motionStart = firstEntityIndex;
                    int instanceCount = chunk.Count;

                    bool hasChunkPhysicsGravityFactorType = chunk.Has(PhysicsGravityFactorType);
                    bool hasChunkPhysicsDampingType = chunk.Has(PhysicsDampingType);
                    bool hasChunkPhysicsMassType = chunk.Has(PhysicsMassType);
                    bool hasChunkPhysicsMassOverrideType = chunk.Has(PhysicsMassOverrideType);

                    // Note: Transform and AngularExpansionFactor could be calculated from PhysicsCollider.MassProperties
                    // However, to avoid the cost of accessing the collider we assume an infinite mass at the origin of a ~1m^3 box.
                    // For better performance with spheres, or better behavior for larger and/or more irregular colliders
                    // you should add a PhysicsMass component to get the true values
                    var defaultPhysicsMass = new PhysicsMass
                    {
                        Transform = RigidTransform.identity,
                        InverseMass = 0.0f,
                        InverseInertia = float3.zero,
                        AngularExpansionFactor = 1.0f,
                    };

                    // Note: if a dynamic body infinite mass then assume no gravity should be applied
                    float defaultGravityFactor = hasChunkPhysicsMassType ? 1.0f : 0.0f;

                    for (int i = 0, motionIndex = motionStart; i < instanceCount; i++, motionIndex++)
                    {
                        var isKinematic = !hasChunkPhysicsMassType || hasChunkPhysicsMassOverrideType && chunkMassOverrides[i].IsKinematic != 0;
                        MotionVelocities[motionIndex] = new MotionVelocity
                        {
                            LinearVelocity = chunkVelocities[i].Linear,
                            AngularVelocity = chunkVelocities[i].Angular,
                            InverseInertia = isKinematic ? defaultPhysicsMass.InverseInertia : chunkMasses[i].InverseInertia,
                            InverseMass = isKinematic ? defaultPhysicsMass.InverseMass : chunkMasses[i].InverseMass,
                            AngularExpansionFactor = hasChunkPhysicsMassType ? chunkMasses[i].AngularExpansionFactor : defaultPhysicsMass.AngularExpansionFactor,
                            GravityFactor = hasChunkPhysicsGravityFactorType ? chunkGravityFactors[i].Value : defaultGravityFactor
                        };

                    }

                    // Note: these defaults assume a dynamic body with infinite mass, hence no damping 
                    var defaultPhysicsDamping = new PhysicsDamping
                    {
                        Linear = 0.0f,
                        Angular = 0.0f,
                    };

                    // Create motion datas
                    for (int i = 0, motionIndex = motionStart; i < instanceCount; i++, motionIndex++)
                    {
                        PhysicsMass mass = hasChunkPhysicsMassType ? chunkMasses[i] : defaultPhysicsMass;
                        PhysicsDamping damping = hasChunkPhysicsDampingType ? chunkDampings[i] : defaultPhysicsDamping;

                        MotionDatas[motionIndex] = new MotionData
                        {
                            WorldFromMotion = new RigidTransform(
                                math.mul(chunkRotations[i].Value, mass.InertiaOrientation),
                                math.rotate(chunkRotations[i].Value, mass.CenterOfMass) + chunkPositions[i].Value
                            ),
                            BodyFromMotion = new RigidTransform(mass.InertiaOrientation, mass.CenterOfMass),
                            LinearDamping = damping.Linear,
                            AngularDamping = damping.Angular
                        };
                    }
                }
            }

            [BurstCompile]
            internal struct CreateJoints : IJobChunk
            {
                [ReadOnly] public ComponentTypeHandle<PhysicsConstrainedBodyPair> ConstrainedBodyPairComponentType;
                [ReadOnly] public ComponentTypeHandle<PhysicsJoint> JointComponentType;
                [ReadOnly] public EntityTypeHandle EntityType;
                [ReadOnly] public NativeArray<RigidBody> RigidBodies;
                [ReadOnly] public int NumDynamicBodies;

                [NativeDisableParallelForRestriction]
                public NativeArray<Joint> Joints;

                public int DefaultStaticBodyIndex;

                public void Execute(ArchetypeChunk chunk, int chunkIndex, int firstEntityIndex)
                {
                    NativeArray<PhysicsConstrainedBodyPair> chunkBodyPair = chunk.GetNativeArray(ConstrainedBodyPairComponentType);
                    NativeArray<PhysicsJoint> chunkJoint = chunk.GetNativeArray(JointComponentType);
                    NativeArray<Entity> chunkEntities = chunk.GetNativeArray(EntityType);

                    int instanceCount = chunk.Count;
                    for (int i = 0; i < instanceCount; i++)
                    {
                        var bodyPair = chunkBodyPair[i];
                        var entityA = bodyPair.EntityA;
                        var entityB = bodyPair.EntityB;
                        Assert.IsTrue(entityA != entityB);

                        PhysicsJoint joint = chunkJoint[i];

                        // TODO find a reasonable way to look up the constraint body indices
                        // - stash body index in a component on the entity? But we don't have random access to Entity data in a job
                        // - make a map from entity to rigid body index? Sounds bad and I don't think there is any NativeArray-based map data structure yet

                        // If one of the entities is null, use the default static entity
                        var pair = new BodyIndexPair
                        {
                            BodyIndexA = entityA == Entity.Null ? DefaultStaticBodyIndex : -1,
                            BodyIndexB = entityB == Entity.Null ? DefaultStaticBodyIndex : -1
                        };

                        // Find the body indices
                        for (int bodyIndex = 0; bodyIndex < RigidBodies.Length; bodyIndex++)
                        {
                            if (entityA != Entity.Null)
                            {
                                if (RigidBodies[bodyIndex].Entity == entityA)
                                {
                                    pair.BodyIndexA = bodyIndex;
                                    if (pair.BodyIndexB >= 0)
                                    {
                                        break;
                                    }
                                }
                            }

                            if (entityB != Entity.Null)
                            {
                                if (RigidBodies[bodyIndex].Entity == entityB)
                                {
                                    pair.BodyIndexB = bodyIndex;
                                    if (pair.BodyIndexA >= 0)
                                    {
                                        break;
                                    }
                                }
                            }
                        }

                        bool isInvalid = false;
                        // Invalid if we have not found the body indices...
                        isInvalid |= (pair.BodyIndexA == -1 || pair.BodyIndexB == -1);
                        // ... or if we are constraining two static bodies
                        // Mark static-static invalid since they are not going to affect simulation in any way.
                        isInvalid |= (pair.BodyIndexA >= NumDynamicBodies && pair.BodyIndexB >= NumDynamicBodies);
                        if (isInvalid)
                        {
                            pair = BodyIndexPair.Invalid;
                        }

                        Joints[firstEntityIndex + i] = new Joint
                        {
                            BodyPair = pair,
                            Entity = chunkEntities[i],
                            EnableCollision = (byte)chunkBodyPair[i].EnableCollision,
                            AFromJoint = joint.BodyAFromJoint.AsMTransform(),
                            BFromJoint = joint.BodyBFromJoint.AsMTransform(),
                            Version = joint.Version,
                            Constraints = joint.GetConstraints()
                        };
                    }
                }
            }
        }

        #endregion

#if !UNITY_ENTITIES_0_12_OR_NEWER
        EntityTypeHandle GetEntityTypeHandle() => new EntityTypeHandle { Value = GetArchetypeChunkEntityType() };
        ComponentTypeHandle<T> GetComponentTypeHandle<T>(bool isReadOnly = false) where T : struct, IComponentData => new ComponentTypeHandle<T> { Value = GetArchetypeChunkComponentType<T>(isReadOnly) };
#endif
    }
}