using Unity.Mathematics;
using UnityEditor;
using UnityEngine;

namespace Game.System.MotionSystem
{
    /// <summary>
    /// Motion Matching과 실제 이동/입력 시스템 사이의 어댑터입니다.
    /// Motion Matching은 root pose 결과만 계산하고, 실제 이동 적용 방식은 이 클래스를 상속한 구현체가 담당합니다.
    /// </summary>
 public abstract class CharacterControllerBase: ScriptableObject
    {
        [Header("Custom input method")] [Tooltip("GameObject with your custom input implementation")]
        public InputCustomizable customInput;
        [HideInInspector]
        public float3 currentMoveInput;
        protected Vector2 CurrentRawInput;
        [HideInInspector]
        public float3 currentForward;
        protected MotionMatching MotionMatching;
        
        //Collisions and physics
        [HideInInspector]
        public bool collisionsEnabled = true;
        [HideInInspector]
        public bool physicsEnabled = true;
        

        /// <summary>
        /// MotionMatching 인스턴스와 custom input 구현체를 연결합니다.
        /// customInput이 없으면 future trajectory를 만들 입력이 없으므로 실행을 중단합니다.
        /// </summary>
        public virtual void Initialize(MonoBehaviour mb)
        {
            if (!customInput)
            {
                Debug.Log("Custom input ERROR. When using the CustomUserInput input type, it is needed to select an IInputCustomizable object which handles it");
                
#if UNITY_EDITOR
                EditorApplication.isPlaying = false;
#elif UNITY_STANDALONE
                Application.Quit();
#endif
            }

            MotionMatching = (MotionMatching)mb;
        }

        /// <summary>
        /// 프로젝트 입력 시스템에서 원본 2D 입력을 가져옵니다.
        /// </summary>
        protected abstract Vector2 GetRawInput();

        /// <summary>
        /// 현재 입력/카메라/AI 기준으로 캐릭터가 바라볼 forward 방향을 계산합니다.
        /// </summary>
        protected abstract Vector3 GetForward(Vector3 input);

        /// <summary>
        /// raw input을 Motion Matching용 이동 벡터로 변환합니다.
        /// </summary>
        protected void ProcessInput()
        {
            currentMoveInput = customInput.HandleCustomInput(CurrentRawInput, MotionMatching.transform);
        }
        
        /// <summary>
        /// PoseSetter가 계산한 root position/rotation을 실제 캐릭터 이동 시스템에 적용합니다.
        /// </summary>
        public abstract void Move(float3 position, quaternion rotation, float time);

        /// <summary>
        /// 매 tick 입력과 바라볼 방향을 갱신합니다.
        /// TrajectoryEstimation은 여기서 갱신된 currentMoveInput/currentForward를 사용합니다.
        /// </summary>
        public virtual void UpdateMotion(float time)
        {
            CurrentRawInput = GetRawInput();
            ProcessInput();
            currentForward = GetForward(currentMoveInput);
        }

        public virtual void ToggleCollisionsAndPhysics(bool physicsEnabled, bool collisionsEnabled)
        {
            ToggleCollisions(collisionsEnabled);
            TogglePhysics(physicsEnabled);

            this.collisionsEnabled = collisionsEnabled;
            this.physicsEnabled = physicsEnabled;
        }

        public virtual bool IsStrafing()
        {
            return false;
        }

        public virtual string DebugExpectedQuery => string.Empty;
        public virtual string DebugQueryReason => string.Empty;
        
        public virtual void OnCollisionEnter(Collision collision){}
        public virtual void OnCollisionExit(Collision collision){}
        public virtual void OnCollisionStay(Collision collision){}
        public virtual void OnTriggerEnter(Collider other){}
        public virtual void OnTriggerExit(Collider other){}
        public virtual void OnTriggerStay(Collider other){}
        
        protected abstract void ToggleCollisions(bool isEnabled);
        protected abstract void TogglePhysics(bool isEnabled);
    }
}
