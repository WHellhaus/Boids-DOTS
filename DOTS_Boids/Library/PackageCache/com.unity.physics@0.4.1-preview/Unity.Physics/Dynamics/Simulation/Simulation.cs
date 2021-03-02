﻿using System;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Jobs.LowLevel.Unsafe;
using Unity.Mathematics;

namespace Unity.Physics
{
    // Holds temporary data in a storage that lives as long as simulation lives
    // and is only re-allocated if necessary.
    public struct SimulationContext : IDisposable
    {
        private int m_NumDynamicBodies;
        private NativeArray<Velocity> m_InputVelocities;

        // Solver stabilization data (it's completely ok to be unallocated)
        [NativeDisableContainerSafetyRestriction]
        private NativeArray<Solver.StabilizationMotionData> m_SolverStabilizationMotionData;
        internal NativeArray<Solver.StabilizationMotionData> SolverStabilizationMotionData => m_SolverStabilizationMotionData.GetSubArray(0, m_NumDynamicBodies);

        internal float TimeStep;

        internal NativeArray<Velocity> InputVelocities => m_InputVelocities.GetSubArray(0, m_NumDynamicBodies);

        internal NativeStream CollisionEventDataStream;
        internal NativeStream TriggerEventDataStream;

        public CollisionEvents CollisionEvents => new CollisionEvents(CollisionEventDataStream, InputVelocities, TimeStep);
        public TriggerEvents TriggerEvents => new TriggerEvents(TriggerEventDataStream);

        private NativeArray<int> WorkItemCount;

        [Obsolete("Reset() has been deprecated. Use the new method that takes SimulationStepInput instead. (RemovedAfter 2020-09-04)")]
        public void Reset(ref PhysicsWorld world)
        {
            var stepInput = new SimulationStepInput
            {
                Gravity = PhysicsStep.Default.Gravity,
                NumSolverIterations = PhysicsStep.Default.SolverIterationCount,
                SolverStabilizationHeuristicSettings = PhysicsStep.Default.SolverStabilizationHeuristicSettings,
                SynchronizeCollisionWorld = PhysicsStep.Default.SynchronizeCollisionWorld > 0,
                TimeStep = 0.02f,
                World = world
            };
            Reset(stepInput);
        }

        // Resets the simulation storage
        // - Reallocates input velocities storage if necessary
        // - Disposes event streams and allocates new ones with a single work item
        // NOTE: Reset or ScheduleReset needs to be called before passing the SimulationContext
        // to a simulation step job. If you don't then you may get initialization errors.
        public void Reset(SimulationStepInput stepInput)
        {
            m_NumDynamicBodies = stepInput.World.NumDynamicBodies;
            if (!m_InputVelocities.IsCreated || m_InputVelocities.Length < m_NumDynamicBodies)
            {
                if (m_InputVelocities.IsCreated)
                {
                    m_InputVelocities.Dispose();
                }
                m_InputVelocities = new NativeArray<Velocity>(m_NumDynamicBodies, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            }

            // Solver stabilization data
            if (stepInput.SolverStabilizationHeuristicSettings.EnableSolverStabilization)
            {
                if (!m_SolverStabilizationMotionData.IsCreated || m_SolverStabilizationMotionData.Length < m_NumDynamicBodies)
                {
                    if (m_SolverStabilizationMotionData.IsCreated)
                    {
                        m_SolverStabilizationMotionData.Dispose();
                    }
                    m_SolverStabilizationMotionData = new NativeArray<Solver.StabilizationMotionData>(m_NumDynamicBodies, Allocator.Persistent, NativeArrayOptions.ClearMemory);
                }
                else if (m_NumDynamicBodies > 0)
                {
                    unsafe
                    {
                        UnsafeUtility.MemClear(m_SolverStabilizationMotionData.GetUnsafePtr(), m_NumDynamicBodies * UnsafeUtility.SizeOf<Solver.StabilizationMotionData>());
                    }
                }
            }

            if (CollisionEventDataStream.IsCreated)
            {
                CollisionEventDataStream.Dispose();
            }
            if (TriggerEventDataStream.IsCreated)
            {
                TriggerEventDataStream.Dispose();
            }
            
            {
                if (!WorkItemCount.IsCreated)
                {
                    WorkItemCount = new NativeArray<int>(new int[] { 1 }, Allocator.Persistent);
                }
                CollisionEventDataStream = new NativeStream(WorkItemCount[0], Allocator.Persistent);
                TriggerEventDataStream = new NativeStream(WorkItemCount[0], Allocator.Persistent);
            }
        }

        // TODO: We need to make a public version of ScheduleReset for use with
        // local simulation calling StepImmediate and chaining jobs over a number
        // of steps. This becomes a problem if new bodies are added to the world
        // between simulation steps.
        // A public version could take the form:
        //         public JobHandle ScheduleReset(ref PhysicsWorld world, JobHandle inputDeps = default)
        //         {
        //             return ScheduleReset(ref world, inputDeps, true);
        //         }
        // However, to make that possible we need a why to allocate InputVelocities within a job.
        // The core simulation does not chain jobs across multiple simulation steps and so 
        // will not hit this issue.
        internal JobHandle ScheduleReset(SimulationStepInput stepInput, JobHandle inputDeps, bool allocateEventDataStreams)
        {
            m_NumDynamicBodies = stepInput.World.NumDynamicBodies;
            if (!m_InputVelocities.IsCreated || m_InputVelocities.Length < m_NumDynamicBodies)
            {
                // TODO: can we find a way to setup InputVelocities within a job?
                if (m_InputVelocities.IsCreated)
                {
                    m_InputVelocities.Dispose();
                }
                m_InputVelocities = new NativeArray<Velocity>(m_NumDynamicBodies, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            }

            // Solver stabilization data
            if (stepInput.SolverStabilizationHeuristicSettings.EnableSolverStabilization)
            {
                if (!m_SolverStabilizationMotionData.IsCreated || m_SolverStabilizationMotionData.Length < m_NumDynamicBodies)
                {
                    if (m_SolverStabilizationMotionData.IsCreated)
                    {
                        m_SolverStabilizationMotionData.Dispose();
                    }
                    m_SolverStabilizationMotionData = new NativeArray<Solver.StabilizationMotionData>(m_NumDynamicBodies, Allocator.Persistent, NativeArrayOptions.ClearMemory);
                }
                else if (m_NumDynamicBodies > 0)
                {
                    unsafe
                    {
                        UnsafeUtility.MemClear(m_SolverStabilizationMotionData.GetUnsafePtr(), m_NumDynamicBodies * UnsafeUtility.SizeOf<Solver.StabilizationMotionData>());
                    }
                }
            }

            var handle = inputDeps;
            if (CollisionEventDataStream.IsCreated)
            {
                handle = CollisionEventDataStream.Dispose(handle);
            }
            if (TriggerEventDataStream.IsCreated)
            {
                handle = TriggerEventDataStream.Dispose(handle);
            }
            if (allocateEventDataStreams)
            {
                if (!WorkItemCount.IsCreated)
                {
                    WorkItemCount = new NativeArray<int>(new int[] { 1 }, Allocator.Persistent);
                }
                handle = NativeStream.ScheduleConstruct(out CollisionEventDataStream, WorkItemCount, handle, Allocator.Persistent);
                handle = NativeStream.ScheduleConstruct(out TriggerEventDataStream, WorkItemCount, handle, Allocator.Persistent);
            }
            return handle;
        }

        public void Dispose()
        {
            if (m_InputVelocities.IsCreated)
            {
                m_InputVelocities.Dispose();
            }

            if (m_SolverStabilizationMotionData.IsCreated)
            {
                m_SolverStabilizationMotionData.Dispose();
            }

            if (CollisionEventDataStream.IsCreated)
            {
                CollisionEventDataStream.Dispose();
            }

            if (TriggerEventDataStream.IsCreated)
            {
                TriggerEventDataStream.Dispose();
            }

            if (WorkItemCount.IsCreated)
            {
                WorkItemCount.Dispose();
            }
        }
    }

    // Temporary data created and destroyed during the step
    internal struct StepContext
    {
        // Built by the scheduler. Groups body pairs into phases in which each
        // body appears at most once, so that the interactions within each phase can be solved
        // in parallel with each other but not with other phases. This is consumed by the
        // ProcessBodyPairsJob, which outputs contact and joint Jacobians.
        public NativeList<DispatchPairSequencer.DispatchPair> PhasedDispatchPairs;

        // Built by the scheduler. Describes the grouping of PhasedBodyPairs
        // which informs how we can schedule the solver jobs and where they read info from.
        public DispatchPairSequencer.SolverSchedulerInfo SolverSchedulerInfo;

        public NativeStream Contacts;
        public NativeStream Jacobians;
    }

    // Default simulation implementation
    public class Simulation : ISimulation
    {
        public SimulationType Type => SimulationType.UnityPhysics;
        public JobHandle FinalSimulationJobHandle => m_StepHandles.FinalExecutionHandle;
        public JobHandle FinalJobHandle => JobHandle.CombineDependencies(FinalSimulationJobHandle, m_StepHandles.FinalDisposeHandle);

        internal StepContext StepContext = new StepContext();

        public CollisionEvents CollisionEvents => SimulationContext.CollisionEvents;

        public TriggerEvents TriggerEvents => SimulationContext.TriggerEvents;

        internal SimulationContext SimulationContext = new SimulationContext();

        private readonly DispatchPairSequencer m_Scheduler = new DispatchPairSequencer();
        private SimulationJobHandles m_StepHandles = new SimulationJobHandles(new JobHandle());

        public void Dispose()
        {
            m_Scheduler.Dispose();
            SimulationContext.Dispose();
        }

        // Steps the simulation immediately on a single thread without spawning any jobs.
        public static void StepImmediate(SimulationStepInput input, ref SimulationContext simulationContext)
        {
            SafetyChecks.CheckFiniteAndPositiveAndThrow(input.TimeStep, nameof(input.TimeStep));
            SafetyChecks.CheckInRangeAndThrow(input.NumSolverIterations, new int2(1, int.MaxValue), nameof(input.NumSolverIterations));

            if (input.World.NumDynamicBodies == 0)
            {
                // No need to do anything, since nothing can move
                return;
            }

            // Inform the context of the timeStep
            simulationContext.TimeStep = input.TimeStep;

            // Find all body pairs that overlap in the broadphase
            var dynamicVsDynamicBodyPairs = new NativeStream(1, Allocator.Temp);
            var dynamicVsStaticBodyPairs = new NativeStream(1, Allocator.Temp);
            {
                var dynamicVsDynamicBodyPairsWriter = dynamicVsDynamicBodyPairs.AsWriter();
                var dynamicVsStaticBodyPairsWriter = dynamicVsStaticBodyPairs.AsWriter();
                input.World.CollisionWorld.FindOverlaps(ref dynamicVsDynamicBodyPairsWriter, ref dynamicVsStaticBodyPairsWriter);
            }

            // Create dispatch pairs
            var dispatchPairs = new NativeList<DispatchPairSequencer.DispatchPair>(Allocator.Temp);
            DispatchPairSequencer.CreateDispatchPairs(ref dynamicVsDynamicBodyPairs, ref dynamicVsStaticBodyPairs,
                input.World.NumDynamicBodies, input.World.Joints, ref dispatchPairs);

            // Apply gravity and copy input velocities
            Solver.ApplyGravityAndCopyInputVelocities(input.World.DynamicsWorld.MotionVelocities,
                simulationContext.InputVelocities, input.TimeStep * input.Gravity);

            // Narrow phase
            var contacts = new NativeStream(1, Allocator.Temp);
            {
                var contactsWriter = contacts.AsWriter();
                NarrowPhase.CreateContacts(ref input.World, dispatchPairs.AsArray(), input.TimeStep, ref contactsWriter);
            }

            // Build Jacobians
            var jacobians = new NativeStream(1, Allocator.Temp);
            {
                var contactsReader = contacts.AsReader();
                var jacobiansWriter = jacobians.AsWriter();
                Solver.BuildJacobians(ref input.World, input.TimeStep, input.Gravity, input.NumSolverIterations,
                    dispatchPairs.AsArray(), ref contactsReader, ref jacobiansWriter);
            }

            // Solve Jacobians
            {
                var jacobiansReader = jacobians.AsReader();
                var collisionEventsWriter = simulationContext.CollisionEventDataStream.AsWriter();
                var triggerEventsWriter = simulationContext.TriggerEventDataStream.AsWriter();
                Solver.StabilizationData solverStabilizationData = new Solver.StabilizationData(input, simulationContext);
                Solver.SolveJacobians(ref jacobiansReader, input.World.DynamicsWorld.MotionVelocities,
                    input.TimeStep, input.NumSolverIterations, ref collisionEventsWriter, ref triggerEventsWriter, solverStabilizationData);
            }

            // Integrate motions
            Integrator.Integrate(input.World.DynamicsWorld.MotionDatas, input.World.DynamicsWorld.MotionVelocities, input.TimeStep);

            // Synchronize the collision world if asked for
            if (input.SynchronizeCollisionWorld)
            {
                input.World.CollisionWorld.UpdateDynamicTree(ref input.World, input.TimeStep, input.Gravity);
            }
        }

        public void Step(SimulationStepInput input)
        {
            StepImmediate(input, ref SimulationContext);
        }

        // Schedule all the jobs for the simulation step.
        // Enqueued callbacks can choose to inject additional jobs at defined sync points.
        // threadCountHint defines which simulation type will be called:
        //     - threadCountHint > 0 will result in default multithreaded simulation
        //     - threadCountHint <=0 will result in a very small number of jobs (1 per physics step phase) that are scheduled sequentially
        // Behavior doesn't change regardless of the threadCountHint provided.
        public unsafe SimulationJobHandles ScheduleStepJobs(SimulationStepInput input, SimulationCallbacks callbacksIn, JobHandle inputDeps, int threadCountHint = 0)
        {
            SafetyChecks.CheckFiniteAndPositiveAndThrow(input.TimeStep, nameof(input.TimeStep));
            SafetyChecks.CheckInRangeAndThrow(input.NumSolverIterations, new int2(1, int.MaxValue), nameof(input.NumSolverIterations));

            bool singleThreadedSim = (threadCountHint <= 0);

            // Dispose and reallocate input velocity buffer, if dynamic body count has increased.
            // Dispose previous collision and trigger event data streams. 
            // New event streams are reallocated later when the work item count is known.
            JobHandle handle = SimulationContext.ScheduleReset(input, inputDeps, false);
            SimulationContext.TimeStep = input.TimeStep;

            StepContext = new StepContext();

            if (input.World.NumDynamicBodies == 0)
            {
                // No need to do anything, since nothing can move
                m_StepHandles = new SimulationJobHandles(handle);
                return m_StepHandles;
            }

            SimulationCallbacks callbacks = callbacksIn ?? new SimulationCallbacks();

            // Find all body pairs that overlap in the broadphase
            var handles = input.World.CollisionWorld.ScheduleFindOverlapsJobs(
                out NativeStream dynamicVsDynamicBodyPairs, out NativeStream dynamicVsStaticBodyPairs, handle, threadCountHint);
            handle = handles.FinalExecutionHandle;
            var disposeHandle1 = handles.FinalDisposeHandle;
            var postOverlapsHandle = handle;

            // Sort all overlapping and jointed body pairs into phases
            handles = m_Scheduler.ScheduleCreatePhasedDispatchPairsJob(
                ref input.World, ref dynamicVsDynamicBodyPairs, ref dynamicVsStaticBodyPairs, handle,
                ref StepContext.PhasedDispatchPairs, out StepContext.SolverSchedulerInfo, threadCountHint);
            handle = handles.FinalExecutionHandle;
            var disposeHandle2 = handles.FinalDisposeHandle;

            // Apply gravity and copy input velocities at this point (in parallel with the scheduler, but before the callbacks)
            var applyGravityAndCopyInputVelocitiesHandle = Solver.ScheduleApplyGravityAndCopyInputVelocitiesJob(
                input.World.DynamicsWorld.MotionVelocities, SimulationContext.InputVelocities,
                input.TimeStep * input.Gravity, singleThreadedSim ? handle : postOverlapsHandle, threadCountHint);

            handle = JobHandle.CombineDependencies(handle, applyGravityAndCopyInputVelocitiesHandle);
            handle = callbacks.Execute(SimulationCallbacks.Phase.PostCreateDispatchPairs, this, ref input.World, handle);

            // Create contact points & joint Jacobians
            handles = NarrowPhase.ScheduleCreateContactsJobs(ref input.World, input.TimeStep,
                ref StepContext.Contacts, ref StepContext.Jacobians, ref StepContext.PhasedDispatchPairs, handle,
                ref StepContext.SolverSchedulerInfo, threadCountHint);
            handle = handles.FinalExecutionHandle;
            var disposeHandle3 = handles.FinalDisposeHandle;
            handle = callbacks.Execute(SimulationCallbacks.Phase.PostCreateContacts, this, ref input.World, handle);

            // Create contact Jacobians
            handles = Solver.ScheduleBuildJacobiansJobs(ref input.World, input.TimeStep, input.Gravity, input.NumSolverIterations,
                handle, ref StepContext.PhasedDispatchPairs, ref StepContext.SolverSchedulerInfo,
                ref StepContext.Contacts, ref StepContext.Jacobians, threadCountHint);
            handle = handles.FinalExecutionHandle;
            var disposeHandle4 = handles.FinalDisposeHandle;
            handle = callbacks.Execute(SimulationCallbacks.Phase.PostCreateContactJacobians, this, ref input.World, handle);

            // Solve all Jacobians
            Solver.StabilizationData solverStabilizationData = new Solver.StabilizationData(input, SimulationContext);
            handles = Solver.ScheduleSolveJacobiansJobs(ref input.World.DynamicsWorld, input.TimeStep, input.NumSolverIterations,
                ref StepContext.Jacobians, ref SimulationContext.CollisionEventDataStream, ref SimulationContext.TriggerEventDataStream,
                ref StepContext.SolverSchedulerInfo, solverStabilizationData, handle, threadCountHint);
            handle = handles.FinalExecutionHandle;
            var disposeHandle5 = handles.FinalDisposeHandle;
            handle = callbacks.Execute(SimulationCallbacks.Phase.PostSolveJacobians, this, ref input.World, handle);

            // Integrate motions
            handle = Integrator.ScheduleIntegrateJobs(ref input.World.DynamicsWorld, input.TimeStep, handle, threadCountHint);

            // Synchronize the collision world
            if (input.SynchronizeCollisionWorld)
            {
                handle = input.World.CollisionWorld.ScheduleUpdateDynamicTree(ref input.World, input.TimeStep, input.Gravity, handle, threadCountHint);  // TODO: timeStep = 0?
            }

            // Return the final simulation handle
            m_StepHandles.FinalExecutionHandle = handle;

            // Different dispose logic for single threaded simulation compared to "standard" threading (multi threaded)
            if (singleThreadedSim)
            {
                handle = dynamicVsDynamicBodyPairs.Dispose(handle);
                handle = dynamicVsStaticBodyPairs.Dispose(handle);
                handle = StepContext.PhasedDispatchPairs.Dispose(handle);
                handle = StepContext.Contacts.Dispose(handle);
                handle = StepContext.Jacobians.Dispose(handle);
                handle = StepContext.SolverSchedulerInfo.ScheduleDisposeJob(handle);

                m_StepHandles.FinalDisposeHandle = handle;
            }
            else
            {
                // Return the final handle, which includes disposing temporary arrays
                JobHandle* deps = stackalloc JobHandle[5]
                {
                    disposeHandle1,
                    disposeHandle2,
                    disposeHandle3,
                    disposeHandle4,
                    disposeHandle5
                };
                m_StepHandles.FinalDisposeHandle = JobHandleUnsafeUtility.CombineDependencies(deps, 5);
            }

            return m_StepHandles;
        }
    }
}
