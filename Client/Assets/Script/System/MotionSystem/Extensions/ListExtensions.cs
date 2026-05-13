using System.Collections.Generic;

namespace Game.System.MotionSystem
{
    /// <summary>
    /// Dataset bake 과정에서 frame index 위치에 데이터를 안전하게 넣기 위한 List 유틸리티입니다.
    /// </summary>
    public static class ListExtensions
    {
        /// <summary>
        /// list[index]가 이미 있으면 교체하고, 아직 없으면 해당 위치에 삽입합니다.
        /// Dataset.SetAnimationBoneData/SetAnimationRootData가 같은 frame 데이터를 여러 번 갱신할 때 사용합니다.
        /// </summary>
        public static void AddOrReplace<T>(this List<T> list, int index, T element)
        {
            if (list.Count > index)
            {
                list[index] = element;
                return;
            }

            list.Insert(index, element);
        }
    }
}
