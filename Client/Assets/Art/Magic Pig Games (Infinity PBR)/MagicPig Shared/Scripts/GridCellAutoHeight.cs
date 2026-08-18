using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace MagicPigGames
{
    [RequireComponent(typeof(GridLayoutGroup))]
    public class GridCellAutoHeight : MonoBehaviour
    {
        [Header("Options")]
        [Tooltip("The grid cell height will be a percentage of the screen height.")]
        public float percentOfHeight = 0.25f;
        [Tooltip("When true, the grid cell height will be updated every frame. Otherwise, just at Start().")]
        public bool updateEveryFrame = false;
        public bool scaleXSpacing = true;
        [Tooltip("Min and Max spacing on the X axis between cells, based on the Screen height (0 = x, full height = y).")]
        public Vector2 xSpacing = Vector2.zero;

        [Header("Transition Options")] 
        [Tooltip("When transitioning the grid cell height, this is the time it will take to complete.")]
        public float transitionTime = 1f;
        
        private GridLayoutGroup _gridLayoutGroup;
        
        private float _cellSizeX = 0f;
        private float _cellSizeY = 0f;
        private float _desiredPercentOfHeight = 0.25f;
        private Coroutine _transitionCoroutine;
        
        private float CellAspectRatio => _cellSizeX / _cellSizeY;
        private float CurrentPercentOfHeight => _gridLayoutGroup.cellSize.y / Screen.height;
        
        private void Start()
        {
            _gridLayoutGroup = GetComponent<GridLayoutGroup>();
            
            _cellSizeY = _gridLayoutGroup.cellSize.y;
            _cellSizeX = _gridLayoutGroup.cellSize.x;

            ScaleHeight();
            _desiredPercentOfHeight = percentOfHeight;
        }

        private void Update()
        {
            if (!updateEveryFrame) return;
            
            ScaleHeight();
        }
        
        private void ScaleHeight()
        {
            var screenHeight = Screen.height;
            var height = screenHeight * percentOfHeight;
            var cellSize = _gridLayoutGroup.cellSize;
            
            cellSize.x = height * CellAspectRatio;
            cellSize.y = height;
            
            _gridLayoutGroup.cellSize = cellSize;
            ScaleXSpacing();
        }

        private void ScaleXSpacing()
        {
            if (!scaleXSpacing) return;
            
            var spacing = Mathf.Lerp(xSpacing.x, xSpacing.y, CurrentPercentOfHeight);
            var gridSpacing = _gridLayoutGroup.spacing;
            gridSpacing.x = spacing;
            _gridLayoutGroup.spacing = gridSpacing;
        }

        public void StartTransition(float newPercentOfHeight)
        {
            _desiredPercentOfHeight = newPercentOfHeight;
            if (_transitionCoroutine != null)
                StopCoroutine(_transitionCoroutine);
            _transitionCoroutine = StartCoroutine(Transition());
        }

        private IEnumerator Transition()
        {
            var t = 0f;
            var startPercentOfHeight = CurrentPercentOfHeight;
            while (t < 1f)
            {
                t += Time.deltaTime / transitionTime;
                percentOfHeight = Mathf.Lerp(startPercentOfHeight, _desiredPercentOfHeight, t);
                ScaleHeight();
                yield return null;
            }
            
            _transitionCoroutine = null;
        }
    }
}