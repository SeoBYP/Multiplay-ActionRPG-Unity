using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using Random = UnityEngine.Random;

namespace InfinityPBR.Demo
{
    public class InfinityDemoCharacter : MonoBehaviour
    {
        
        [Header("Components")]
        public Animator animator;
        public SkinnedMeshRenderer skinnedMeshRenderer;

        [Header("Options")]
        public KeyCode superRandomKey = KeyCode.R;
        public Button superRandomButton;
        public Button resetButton;
        
        [Header("Randomization")]
        public bool automateStyles = true;
        public float randomSeedChangeSpeedMin = 1f; // How fast the random seed changes
        public float randomSeedChangeSpeedMax = 3f; // How fast the random seed changes
        public float randomSeedChangePeriodMin = 1.5f; // Period between changes
        public float randomSeedChangePeriodMax = 4f; // Period between changes
        
        [Header("Demo Stuff")] 
        public string[] animationTriggers;
        public GameObject animationFloatPrefab;
        public GameObject animationButtonPrefab;
        public GameObject animationButtonContainer;
        public GameObject automateStyleToggle;
        public string[] styleKeys; // Keys of float styles that will be automated.
        public string[] keysToExclude; // These keys will not load in the demo scene.
        public bool usingAutomatedStyles = true;
        public Collider playArea;
        
        // Privates
        private static readonly int Locomotion = Animator.StringToHash("Locomotion");
        private int _animationTriggerIndex;
        
        private float _randomValue;
        private float _randomFrom;
        private float _randomSeed;
        private float _randomLerp;
        private bool _randomWait;
        private float _randomSeedChangeSpeed = 1f;
        private float _randomSeedChangePeriod = 1f;
        protected Vector3 _startPosition;
        protected Quaternion _startRotation;
        protected Transform _transform;
        
        private BlendShapesPresetManager _blendShapesPresetManager;
        public BlendShapesPresetManager BlendShapesPresetManager => _blendShapesPresetManager;
        
        // Start is called before the first frame update
        public virtual void Start()
        {
            _blendShapesPresetManager = GetComponent<BlendShapesPresetManager>();
            _transform = GetComponent<Transform>();
            _startPosition = _transform.localPosition;
            _startRotation = _transform.localRotation;
            
            if (automateStyles) CreateStyleToggle();
            PopulateAnimationButtons();
            
            
            
            if (automateStyles) StartCoroutine(nameof(Randomize));
        }

        public void Update()
        {
            if (Input.GetKeyDown(KeyCode.RightArrow)) SetAnimation(_animationTriggerIndex += 1);
            if (Input.GetKeyDown(KeyCode.LeftArrow)) SetAnimation(_animationTriggerIndex -= 1);
            if (Input.GetKeyDown(KeyCode.Space)) TriggerAnimation();
            if (Input.GetKeyDown(superRandomKey) && !ShiftIsDown())
            {
                if (superRandomButton != null)
                    superRandomButton.onClick.Invoke();
                else
                    ManualSuperRandom();
            }
            if (Input.GetKeyDown(superRandomKey) && ShiftIsDown())
            {
                if (resetButton != null)
                    resetButton.onClick.Invoke();
                else
                    ManualReset();
            }

            CheckPlayArea();
        }

        protected virtual void ManualSuperRandom() => BlendShapesPresetManager.StartTransitionToPreset("Random");

        protected virtual void ManualReset() => BlendShapesPresetManager.StartTransitionToPreset("Reset");

        protected virtual void CheckPlayArea()
        {
            if (playArea == null) return;

            if (playArea.bounds.Contains(_transform.position)) return;
            
            ResetPositionAndRotation();
        }

        protected virtual void ResetPositionAndRotation()
        {
            ResetPosition();
            ResetRotation();
        }

        protected virtual void ResetRotation() => _transform.localRotation = _startRotation;
        protected virtual void ResetPosition() => _transform.localPosition = _startPosition;

        IEnumerator Randomize()
        {
            while (true)
            {
                while (!automateStyles)
                    yield return null;

                while (_randomLerp < 1f)
                {
                    if (_randomLerp <= 0) _randomSeed = Random.Range(_randomSeed < 0.5 ? 0.5f : 0f, _randomSeed >= 0.5 ? 0.5f : 1f); // Set new random value
                    
                    _randomLerp += Time.deltaTime / Random.Range(randomSeedChangeSpeedMin, randomSeedChangeSpeedMax);
                    _randomValue = Mathf.Lerp(_randomFrom, _randomSeed, _randomLerp);
                    SetStyleValue(_randomValue);
                    yield return null;
                }
                
                _randomLerp = 0f; // Reset
                _randomFrom = _randomSeed; // Cache the value
                yield return new WaitForSeconds(Random.Range(randomSeedChangePeriodMin, randomSeedChangePeriodMax)); // Wait for the next period
            }
        }

        
        private void SetStyleValue(float randomValue)
        {
            foreach (var key in styleKeys)
                animator.SetFloat(key, randomValue);
        }
        
        private void SetAnimation(int newIndex, bool trigger = true)
        {
            if (TriggerParameters().Length == 0) return;
            if (newIndex < 0) newIndex = 0;
            if (newIndex >= TriggerParameters().Length) newIndex = TriggerParameters().Length - 1;
            _animationTriggerIndex = newIndex;
            
            Debug.Log($"Animation trigger is <color=#ff00ff>{TriggerParameters()[newIndex].name}</color>");
            
            if (!trigger || ShiftIsDown()) return;
            TriggerAnimation();
        }

        private bool ShiftIsDown() => Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);

        private void TriggerAnimation()
        {
            if (animator == null) return;
            animator.SetTrigger(TriggerParameters()[_animationTriggerIndex].name);
        }
        
        // Animator Component Stuff
        public virtual void SetLocomotion(float value) => animator.SetFloat(Locomotion, value);

        // This adds the animation trigger buttons to the demo scene
        private void PopulateAnimationButtons()
        {
            foreach (var parameter in FloatParameters())
                CreateFloatSlider(parameter.name);
            foreach (var parameter in TriggerParameters())
                CreateTriggerButton(parameter.name);
        }

        private AnimatorControllerParameter[] TriggerParameters() => animator.parameters
            .Where(x => !keysToExclude.Contains(x.name))
            .Where(x => x.type == AnimatorControllerParameterType.Trigger)
            .ToArray();
        
        private AnimatorControllerParameter[] FloatParameters() => animator.parameters
            .Where(x => !keysToExclude.Contains(x.name))
            .Where(x => x.type == AnimatorControllerParameterType.Float)
            .ToArray();

        // Adds a single button
        private void CreateStyleToggle()
        {
            if (automateStyleToggle == null) return;
            if (animationButtonContainer == null) return;
            var newToggle = Instantiate(automateStyleToggle, animationButtonContainer.transform);
            newToggle.GetComponent<InfinityDemoAutomateStyles>().Setup(this);
        }

        
        // Adds a single button
        private void CreateTriggerButton(string trigger)
        {
            if (animationButtonPrefab == null) return;
            if (animationButtonContainer == null) return;
            var newTrigger = Instantiate(animationButtonPrefab, animationButtonContainer.transform);
            newTrigger.name = trigger;
            newTrigger.GetComponent<InfinityDemoAnimationButton>().Setup(trigger, animator);
        }
        
        // Adds a single float slider
        private void CreateFloatSlider(string key)
        {
            if (animationFloatPrefab == null) return;
            if (animationButtonContainer == null) return;
            var newSlider = Instantiate(animationFloatPrefab, animationButtonContainer.transform);
            newSlider.name = key;
            newSlider.GetComponent<InfinityDemoFloatSlider>().Setup(key, animator);
        }
        
        public void InvokeRandomButton(Button[] array) => array[Random.Range(0, array.Length)].onClick.Invoke();

        public virtual void NextMaterial()
        {
            
        }

        public virtual void PreviousMaterial()
        {
            
        }
    }
}
