#if LEGACY_PHYSICS
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using FloatRange = Unity.Physics.Math.FloatRange;
using LegacyCharacter = UnityEngine.CharacterJoint;
using LegacyConfigurable = UnityEngine.ConfigurableJoint;
using LegacyFixed = UnityEngine.FixedJoint;
using LegacyHinge = UnityEngine.HingeJoint;
using LegacyJoint = UnityEngine.Joint;
using LegacySpring = UnityEngine.SpringJoint;

namespace Unity.Physics.Authoring
{
    [AlwaysUpdateSystem]
    [UpdateAfter(typeof(BeginJointConversionSystem))]
    [UpdateBefore(typeof(EndJointConversionSystem))]
    [EditorBrowsable(EditorBrowsableState.Never)]
    [Obsolete("This class will be made internal in a future version. Please use "
              + nameof(BeginJointConversionSystem)
              + " and "
              + nameof(EndJointConversionSystem)
              + " as needed to express dependencies on built-in joint conversion. (RemovedAfter 2020-08-18)")]
    public sealed class LegacyJointConversionSystem : GameObjectConversionSystem
    {
        PhysicsJoint CreateConfigurableJoint(
            quaternion jointFrameOrientation,
            LegacyJoint joint, bool3 linearLocks, bool3 linearLimited, SoftJointLimit linearLimit, SoftJointLimitSpring linearSpring, bool3 angularFree, bool3 angularLocks,
            bool3 angularLimited, SoftJointLimit lowAngularXLimit, SoftJointLimit highAngularXLimit, SoftJointLimitSpring angularXLimitSpring, SoftJointLimit angularYLimit,
            SoftJointLimit angularZLimit, SoftJointLimitSpring angularYZLimitSpring)
        {
            var constraints = new FixedList128<Constraint>();

            // TODO: investigate mapping PhysX spring and damping to Unity Physics SpringFrequency and SpringDamping
            var springFrequency = Constraint.DefaultSpringFrequency;
            var springDamping = Constraint.DefaultSpringDamping;
            
            if (angularLimited[0])
            {
                constraints.Add(Constraint.Twist(0, math.radians(new FloatRange(-highAngularXLimit.limit, -lowAngularXLimit.limit).Sorted()), springFrequency, springDamping));
            }

            if (angularLimited[1])
            {
                constraints.Add(Constraint.Twist(1, math.radians(new FloatRange(-angularYLimit.limit, angularYLimit.limit).Sorted()), springFrequency, springDamping));
            }

            if (angularLimited[2])
            {
                constraints.Add(Constraint.Twist(2, math.radians(new FloatRange(-angularZLimit.limit, angularZLimit.limit).Sorted()), springFrequency, springDamping));
            }

            if (math.any(linearLimited))
            {
                var distanceRange = new FloatRange(-linearLimit.limit, linearLimit.limit).Sorted();
                constraints.Add(new Constraint
                {
                    ConstrainedAxes = linearLimited,
                    Type = ConstraintType.Linear,
                    Min = math.csum((int3)linearLimited) == 1 ? distanceRange.Min : 0f,
                    Max = distanceRange.Max,
                    SpringFrequency = springFrequency,
                    SpringDamping = springDamping
                });
            }

            if (math.any(linearLocks))
            {
                constraints.Add(new Constraint
                {
                    ConstrainedAxes = linearLocks,
                    Type = ConstraintType.Linear,
                    Min = 0,
                    Max = 0,
                    SpringFrequency = springFrequency,
                    SpringDamping = springDamping
                });
            }

            if (math.any(angularLocks))
            {
                constraints.Add(new Constraint
                {
                    ConstrainedAxes = angularLocks,
                    Type = ConstraintType.Angular,
                    Min = 0,
                    Max = 0,
                    SpringFrequency = springFrequency,
                    SpringDamping = springDamping
                });
            }

            RigidTransform worldFromBodyA = Math.DecomposeRigidBodyTransform(joint.transform.localToWorldMatrix);
            RigidTransform worldFromBodyB = joint.connectedBody == null
                ? RigidTransform.identity
                : Math.DecomposeRigidBodyTransform(joint.connectedBody.transform.localToWorldMatrix);

            var legacyWorldFromJointA = math.mul(
                new RigidTransform(joint.transform.rotation, joint.transform.position),
                new RigidTransform(jointFrameOrientation, joint.anchor)
            );
            var bodyAFromJoint = new BodyFrame(math.mul(math.inverse(worldFromBodyA), legacyWorldFromJointA));
            
            var connectedEntity = GetPrimaryEntity(joint.connectedBody);
            var isConnectedBodyConverted =
                joint.connectedBody == null || connectedEntity != Entity.Null;
           
            RigidTransform bFromA = isConnectedBodyConverted ? math.mul(math.inverse(worldFromBodyB), worldFromBodyA) : worldFromBodyA;
            RigidTransform bFromBSource =
                isConnectedBodyConverted ? RigidTransform.identity : worldFromBodyB;

            var bodyBFromJoint = new BodyFrame
            {
                Axis = math.mul(bFromA.rot, bodyAFromJoint.Axis),
                PerpendicularAxis = math.mul(bFromA.rot, bodyAFromJoint.PerpendicularAxis),
                Position = math.mul(bFromBSource, new float4(joint.connectedAnchor, 1f)).xyz
            };

            var jointData = new PhysicsJoint
            {
                BodyAFromJoint = bodyAFromJoint,
                BodyBFromJoint = bodyBFromJoint
            };
            jointData.SetConstraints(constraints);
            return jointData;
        }

        bool3 GetAxesWithMotionType(
            ConfigurableJointMotion motionType,
            ConfigurableJointMotion x, ConfigurableJointMotion y, ConfigurableJointMotion z
        ) =>
            new bool3(x == motionType, y == motionType, z == motionType);

        PhysicsConstrainedBodyPair GetConstrainedBodyPair(LegacyJoint joint) =>
            new PhysicsConstrainedBodyPair(
                GetPrimaryEntity(joint.gameObject),
                joint.connectedBody == null ? Entity.Null : GetPrimaryEntity(joint.connectedBody),
                joint.enableCollision
            );

        void ConvertConfigurableJoint(LegacyConfigurable joint)
        {
            var linearLocks =
                GetAxesWithMotionType(ConfigurableJointMotion.Locked, joint.xMotion, joint.yMotion, joint.zMotion);
            var linearLimited =
                GetAxesWithMotionType(ConfigurableJointMotion.Limited, joint.xMotion, joint.yMotion, joint.zMotion);
            var angularFree =
                GetAxesWithMotionType(ConfigurableJointMotion.Free, joint.angularXMotion, joint.angularYMotion, joint.angularZMotion);
            var angularLocks =
                GetAxesWithMotionType(ConfigurableJointMotion.Locked, joint.angularXMotion, joint.angularYMotion, joint.angularZMotion);
            var angularLimited =
                GetAxesWithMotionType(ConfigurableJointMotion.Limited, joint.angularXMotion, joint.angularYMotion, joint.angularZMotion);

            var jointFrameOrientation = GetJointFrameOrientation(joint.axis, joint.secondaryAxis);
            var jointData = CreateConfigurableJoint(jointFrameOrientation, joint, linearLocks, linearLimited, joint.linearLimit, joint.linearLimitSpring, angularFree, angularLocks, angularLimited,
                joint.lowAngularXLimit, joint.highAngularXLimit, joint.angularXLimitSpring, joint.angularYLimit, joint.angularZLimit, joint.angularYZLimitSpring);
            
            m_EndJointConversionSystem.CreateJointEntity(joint, GetConstrainedBodyPair(joint), jointData);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static quaternion GetJointFrameOrientation(float3 axis, float3 secondaryAxis)
        {
            // classic Unity uses a different approach than BodyFrame.ValidateAxes() for ortho-normalizing degenerate inputs
            // ortho-normalizing here ensures behavior is consistent with classic Unity
            var a = (Vector3)axis;
            var p = (Vector3)secondaryAxis;
            Vector3.OrthoNormalize(ref a, ref p);
            return new BodyFrame { Axis = a, PerpendicularAxis = p }.AsRigidTransform().rot;
        }

        void ConvertCharacterJoint(LegacyCharacter joint)
        {
            var linearLocks = new bool3(true);
            var linearLimited = new bool3(false);

            var angularFree = new bool3(false);
            var angularLocks = new bool3(false);
            var angularLimited = new bool3(true);

            var jointFrameOrientation = GetJointFrameOrientation(joint.axis, joint.swingAxis);
            var jointData = CreateConfigurableJoint(jointFrameOrientation, joint, linearLocks, linearLimited, new SoftJointLimit { limit = 0f, bounciness = 0f }, new SoftJointLimitSpring { spring = 0f, damper = 0f }, angularFree, angularLocks, angularLimited,
                joint.lowTwistLimit, joint.highTwistLimit, joint.twistLimitSpring, joint.swing1Limit, joint.swing2Limit, joint.swingLimitSpring);

            m_EndJointConversionSystem.CreateJointEntity(joint, GetConstrainedBodyPair(joint), jointData);
        }

        void ConvertSpringJoint(LegacySpring joint)
        {
            var distanceRange = new FloatRange(joint.minDistance, joint.maxDistance).Sorted();
            var constraint = new Constraint {
                ConstrainedAxes = new bool3(true),
                Type = ConstraintType.Linear,
                Min = distanceRange.Min,
                Max = distanceRange.Max,
                SpringFrequency = 1f, // ?
                SpringDamping = 0.1f // ?
            };

            var jointFrameA = BodyFrame.Identity;
            jointFrameA.Position = joint.anchor;

            var connectedEntity = GetPrimaryEntity(joint.connectedBody);
            
            var isConnectedBodyConverted =
                joint.connectedBody == null || connectedEntity != Entity.Null;

            RigidTransform bFromBSource =
                isConnectedBodyConverted ? RigidTransform.identity : Math.DecomposeRigidBodyTransform(joint.connectedBody.transform.localToWorldMatrix);

            var jointFrameB = BodyFrame.Identity;
            jointFrameB.Position = math.mul(bFromBSource, new float4(joint.connectedAnchor, 1f)).xyz;

            var jointData = new PhysicsJoint
            {
                BodyAFromJoint = jointFrameA,
                BodyBFromJoint = jointFrameB
            };
            jointData.SetConstraints(new FixedList128<Constraint>
            {
                Length = 1,
                [0] = constraint
            });

            m_EndJointConversionSystem.CreateJointEntity(joint, GetConstrainedBodyPair(joint), jointData);
        }

        void ConvertFixedJoint(LegacyFixed joint)
        {
            var legacyWorldFromJointA = math.mul(
                new RigidTransform(joint.transform.rotation, joint.transform.position),
                new RigidTransform(quaternion.identity, joint.anchor)
            );

            RigidTransform worldFromBodyA = Math.DecomposeRigidBodyTransform(joint.transform.localToWorldMatrix);
            var connectedEntity = GetPrimaryEntity(joint.connectedBody);
            RigidTransform worldFromBodyB = connectedEntity == Entity.Null
                ? RigidTransform.identity
                : Math.DecomposeRigidBodyTransform(joint.connectedBody.transform.localToWorldMatrix);

            var bodyAFromJoint = new BodyFrame(math.mul(math.inverse(worldFromBodyA), legacyWorldFromJointA));
            var bodyBFromJoint = new BodyFrame(math.mul(math.inverse(worldFromBodyB), legacyWorldFromJointA));

            var jointData = PhysicsJoint.CreateFixed(bodyAFromJoint, bodyBFromJoint);

            m_EndJointConversionSystem.CreateJointEntity(joint, GetConstrainedBodyPair(joint), jointData);
        }

        void ConvertHingeJoint(LegacyHinge joint)
        {
            RigidTransform worldFromBodyA = Math.DecomposeRigidBodyTransform(joint.transform.localToWorldMatrix);
            RigidTransform worldFromBodyB = joint.connectedBody == null
                ? RigidTransform.identity
                : Math.DecomposeRigidBodyTransform(joint.connectedBody.transform.localToWorldMatrix);

            Math.CalculatePerpendicularNormalized(joint.axis, out float3 perpendicularA, out _);
            var bodyAFromJoint = new BodyFrame
            {
                Axis = joint.axis,
                PerpendicularAxis = perpendicularA,
                Position = joint.anchor
            };

            var connectedEntity = GetPrimaryEntity(joint.connectedBody);
            var isConnectedBodyConverted =
                joint.connectedBody == null || connectedEntity != Entity.Null;
           
            RigidTransform bFromA = isConnectedBodyConverted ? math.mul(math.inverse(worldFromBodyB), worldFromBodyA) : worldFromBodyA;
            RigidTransform bFromBSource =
                isConnectedBodyConverted ? RigidTransform.identity : worldFromBodyB;

            var bodyBFromJoint = new BodyFrame
            {
                Axis = math.mul(bFromA.rot, joint.axis),
                PerpendicularAxis = math.mul(bFromA.rot, perpendicularA),
                Position = math.mul(bFromBSource, new float4(joint.connectedAnchor, 1f)).xyz
            };

            var limits = math.radians(new FloatRange(joint.limits.min, joint.limits.max).Sorted());
            var jointData = joint.useLimits
                ? PhysicsJoint.CreateLimitedHinge(bodyAFromJoint, bodyBFromJoint, limits)
                : PhysicsJoint.CreateHinge(bodyAFromJoint, bodyBFromJoint);

            m_EndJointConversionSystem.CreateJointEntity(joint, GetConstrainedBodyPair(joint), jointData);
        }

        EndJointConversionSystem m_EndJointConversionSystem;

        protected override void OnCreate()
        {
            base.OnCreate();

            m_EndJointConversionSystem = World.GetOrCreateSystem<EndJointConversionSystem>();
        }

        static readonly List<LegacyCharacter> s_CharacterJointInstances = new List<CharacterJoint>(8);
        static readonly List<LegacyConfigurable> s_ConfigurableJointInstances = new List<ConfigurableJoint>(8);
        static readonly List<LegacyFixed> s_FixedJointInstances = new List<FixedJoint>(8);
        static readonly List<LegacyHinge> s_HingeJointInstances = new List<HingeJoint>(8);
        static readonly List<LegacySpring> s_SpringJointInstances = new List<SpringJoint>(8);

        protected override void OnUpdate()
        {
            Entities.ForEach((LegacyCharacter joint) =>
            {
                joint.gameObject.GetComponents(s_CharacterJointInstances);
                foreach (var instance in s_CharacterJointInstances)
                    ConvertCharacterJoint(instance);
            });
            Entities.ForEach((LegacyConfigurable joint) =>
            {
                joint.gameObject.GetComponents(s_ConfigurableJointInstances);
                foreach (var instance in s_ConfigurableJointInstances)
                    ConvertConfigurableJoint(instance);
            });
            Entities.ForEach((LegacyFixed joint) =>
            {
                joint.gameObject.GetComponents(s_FixedJointInstances);
                foreach (var instance in s_FixedJointInstances)
                    ConvertFixedJoint(instance);
            });
            Entities.ForEach((LegacyHinge joint) =>
            {
                joint.gameObject.GetComponents(s_HingeJointInstances);
                foreach (var instance in s_HingeJointInstances)
                    ConvertHingeJoint(instance);
            });
            Entities.ForEach((LegacySpring joint) =>
            {
                joint.gameObject.GetComponents(s_SpringJointInstances);
                foreach (var instance in s_SpringJointInstances)
                    ConvertSpringJoint(instance);
            });

            s_CharacterJointInstances.Clear();
            s_ConfigurableJointInstances.Clear();
            s_FixedJointInstances.Clear();
            s_HingeJointInstances.Clear();
            s_SpringJointInstances.Clear();
        }
    }
}
#endif
