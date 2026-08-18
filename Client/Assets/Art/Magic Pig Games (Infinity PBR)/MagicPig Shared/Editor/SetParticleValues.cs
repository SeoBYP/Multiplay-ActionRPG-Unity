using UnityEditor;
using UnityEngine;

namespace MagicPigGames.Shared
{
    public class SetParticleValues : EditorWindow
    {
        // ------------------------------------------------------------------
        // LOOPING
        // ------------------------------------------------------------------

        [MenuItem("Window/Magic Pig Games/Set Particles/Set Looping/True")]
        private static void SetLoopingTrue()
        {
            foreach (var obj in Selection.gameObjects)
                SetLooping(obj, true);

            Debug.Log("Particle system looping set to true for all selected objects and their children.");
        }

        [MenuItem("Window/Magic Pig Games/Set Particles/Set Looping/False")]
        private static void SetLoopingFalse()
        {
            foreach (var obj in Selection.gameObjects)
                SetLooping(obj, false);

            Debug.Log("Particle system looping set to false for all selected objects and their children.");
        }

        private static void SetLooping(GameObject obj, bool value)
        {
            // Apply the change to the current object if it has a ParticleSystem component
            var particleSystem = obj.GetComponent<ParticleSystem>();
            if (particleSystem != null)
            {
                var mainModule = particleSystem.main;
                mainModule.loop = value;
            }

            // Recursively apply the change to all child objects
            foreach (Transform child in obj.transform)
                SetLooping(child.gameObject, value);
        }

        // ------------------------------------------------------------------
        // PRE WARM
        // ------------------------------------------------------------------

        [MenuItem("Window/Magic Pig Games/Set Particles/Set Pre Warm/True")]
        private static void SetPreWarmTrue()
        {
            foreach (var obj in Selection.gameObjects)
                SetPreWarm(obj, true);

            Debug.Log("Particle system Pre warm set to true for all selected objects and their children.");
        }

        [MenuItem("Window/Magic Pig Games/Set Particles/Set Pre Warm/False")]
        private static void SetPreWarmFalse()
        {
            foreach (var obj in Selection.gameObjects)
                SetPreWarm(obj, false);

            Debug.Log("Particle system Pre warm set to true for all selected objects and their children.");
        }

        private static void SetPreWarm(GameObject obj, bool value)
        {
            // Apply the change to the current object if it has a ParticleSystem component
            var particleSystem = obj.GetComponent<ParticleSystem>();
            if (particleSystem != null)
            {
                var mainModule = particleSystem.main;
                mainModule.prewarm = value;
            }

            // Recursively apply the change to all child objects
            foreach (Transform child in obj.transform)
                SetPreWarm(child.gameObject, value);
        }

        // ------------------------------------------------------------------
        // SIMULATION SPACE
        // ------------------------------------------------------------------

        [MenuItem("Window/Magic Pig Games/Set Particles/Set Simulation Space/Local")]
        private static void SetSimulationSpaceToLocal()
        {
            foreach (var obj in Selection.gameObjects)
                SetSimulationSpace(obj, ParticleSystemSimulationSpace.Local);

            Debug.Log("Particle system simulation space set to Local for all selected objects and their children.");
        }

        [MenuItem("Window/Magic Pig Games/Set Particles/Set Simulation Space/World")]
        private static void SetSimulationSpaceToWorld()
        {
            foreach (var obj in Selection.gameObjects)
                SetSimulationSpace(obj, ParticleSystemSimulationSpace.World);

            Debug.Log("Particle system simulation space set to World for all selected objects and their children.");
        }

        private static void SetSimulationSpace(GameObject obj, ParticleSystemSimulationSpace value)
        {
            // Apply the change to the current object if it has a ParticleSystem component
            var particleSystem = obj.GetComponent<ParticleSystem>();
            if (particleSystem != null)
            {
                var mainModule = particleSystem.main;
                mainModule.simulationSpace = value;
            }

            // Recursively apply the change to all child objects
            foreach (Transform child in obj.transform)
                SetSimulationSpace(child.gameObject, value);
        }

        // ------------------------------------------------------------------
        // PLAY ON AWAKE
        // ------------------------------------------------------------------

        [MenuItem("Window/Magic Pig Games/Set Particles/Set Play On Awake/True")]
        private static void SetPlayOnAwakeTrue()
        {
            foreach (var obj in Selection.gameObjects)
                SetPlayOnAwake(obj, true);

            Debug.Log("Particle system Play On Awake set to true for all selected objects and their children.");
        }

        [MenuItem("Window/Magic Pig Games/Set Particles/Set Play On Awake/False")]
        private static void SetPlayOnAwakeFalse()
        {
            foreach (var obj in Selection.gameObjects)
                SetPlayOnAwake(obj, false);

            Debug.Log("Particle system Play On Awake set to false for all selected objects and their children.");
        }

        private static void SetPlayOnAwake(GameObject obj, bool value)
        {
            // Apply the change to the current object if it has a ParticleSystem component
            var particleSystem = obj.GetComponent<ParticleSystem>();
            if (particleSystem != null)
            {
                var mainModule = particleSystem.main;
                mainModule.playOnAwake = value;
            }

            // Recursively apply the change to all child objects
            foreach (Transform child in obj.transform)
                SetPlayOnAwake(child.gameObject, value);
        }

        // ------------------------------------------------------------------
        // EMITTER VELOCITY MODE
        // ------------------------------------------------------------------
        
        [MenuItem("Window/Magic Pig Games/Set Particles/Set Emitter Velocity Mode/Transform")]
        private static void SetEmitterVelocityModeTransform()
        {
            foreach (var obj in Selection.gameObjects)
                SetEmitterVelocityMode(obj, ParticleSystemEmitterVelocityMode.Transform);

            Debug.Log("Particle system Emitter Velocity Mode set to Transform for all selected objects and their children.");
        }
        
        [MenuItem("Window/Magic Pig Games/Set Particles/Set Emitter Velocity Mode/Rigidbody")]
        private static void SetEmitterVelocityModeRigidbody()
        {
            foreach (var obj in Selection.gameObjects)
                SetEmitterVelocityMode(obj, ParticleSystemEmitterVelocityMode.Rigidbody);

            Debug.Log("Particle system Emitter Velocity Mode set to Rigidbody for all selected objects and their children.");
        }
        
        [MenuItem("Window/Magic Pig Games/Set Particles/Set Emitter Velocity Mode/Both")]
        private static void SetEmitterVelocityModeBoth()
        {
            foreach (var obj in Selection.gameObjects)
                SetEmitterVelocityMode(obj, ParticleSystemEmitterVelocityMode.Transform);
            
            Debug.Log("Particle system Emitter Velocity Mode set to Both for all selected objects and their children.");
        }
        
        private static void SetEmitterVelocityMode(GameObject obj, ParticleSystemEmitterVelocityMode value)
        {
            // Apply the change to the current object if it has a ParticleSystem component
            var particleSystem = obj.GetComponent<ParticleSystem>();
            if (particleSystem != null)
            {
                var mainModule = particleSystem.main;
                mainModule.emitterVelocityMode = value;
            }

            // Recursively apply the change to all child objects
            foreach (Transform child in obj.transform)
                SetEmitterVelocityMode(child.gameObject, value);
        }
        
        // ------------------------------------------------------------------
        // START SIZE MULTIPLIER x3 and /3 HANDLES MIN AND MAX AS WELL AS SINGLE VALUE
        // ------------------------------------------------------------------

        [MenuItem("Window/Magic Pig Games/Set Particles/Set Start Size Multiplier/x3")]
        private static void SetStartSizeMultiplierX3()
        {
            foreach (var obj in Selection.gameObjects)
                SetStartSizeMultiplier(obj, 3);

            Debug.Log("Particle system Start Size Multiplier set to x3 for all selected objects and their children.");
        }

        [MenuItem("Window/Magic Pig Games/Set Particles/Set Start Size Multiplier/x0.33")]
        private static void SetStartSizeMultiplierDiv3()
        {
            foreach (var obj in Selection.gameObjects)
                SetStartSizeMultiplier(obj, 1f / 3f);

            Debug.Log("Particle system Start Size Multiplier set to /3 for all selected objects and their children.");
        }

        private static void SetStartSizeMultiplier(GameObject obj, float value)
        {
            // Apply the change to the current object if it has a ParticleSystem component
            var particleSystem = obj.GetComponent<ParticleSystem>();
            if (particleSystem != null)
            {
                var mainModule = particleSystem.main;
                var startSize = mainModule.startSize;

                if (startSize.mode == ParticleSystemCurveMode.TwoConstants)
                {
                    startSize.constantMin = startSize.constantMin * value;
                    startSize.constantMax = startSize.constantMax * value;
                }
                else
                {
                    startSize.constant = startSize.constant * value;
                }

                mainModule.startSize = startSize;
            }

            // Recursively apply the change to all child objects
            foreach (Transform child in obj.transform)
                SetStartSizeMultiplier(child.gameObject, value);
        }
    }
}