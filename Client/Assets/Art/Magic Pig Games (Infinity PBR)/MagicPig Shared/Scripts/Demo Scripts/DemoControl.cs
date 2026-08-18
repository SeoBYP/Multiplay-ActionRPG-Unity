using UnityEngine;
using UnityEngine.UI;

namespace InfinityPBR.Demo
{
	public class DemoControl : MonoBehaviour
	{

		[Header("Plumbing")]
		public GameObject cameraObject;
		public GameObject canvas;
		public Slider heightBar;
		public Button gotHitDirectionalButton;

		[Header("Options")]
		public float mouseSensitivityY = -0.005f;
		public float mouseSensitivityX = 0.01f;
		public float timeScaleIncrement = 0.1f;
		public KeyCode gotHitDirectionalKey = KeyCode.G;

		// privates
		private Vector2 lastMousePosition;

		void Update()
		{
			if (Input.GetKeyDown(KeyCode.Minus)) SetTimescale(Time.timeScale -= timeScaleIncrement);
			if (Input.GetKeyDown(KeyCode.Plus)) SetTimescale(Time.timeScale += timeScaleIncrement);
			if (Input.GetKeyDown(KeyCode.C)) canvas.SetActive(!canvas.activeSelf);
			if (Input.GetKeyDown(gotHitDirectionalKey)) gotHitDirectionalButton.onClick.Invoke();
		}

		/// <summary>
		/// Simply sets the .y value of the camera transform
		/// </summary>
		/// <param name="newValue">New value.</param>
		public void SetCameraHeight(float newValue)
		{
			//Debug.Log("Old:" + cameraObject.transform.localPosition);
			Vector3 newPosition = new Vector3(cameraObject.transform.localPosition.x, newValue, cameraObject.transform.localPosition.z);
			cameraObject.transform.localPosition = newPosition;			// Set the position
			//Debug.Log("New: " + cameraObject.transform.localPosition);
		}

		/// <summary>
		/// Simply sets the timescale
		/// </summary>
		/// <param name="newValue">New value.</param>
		public void SetTimescale(float newValue){
			Time.timeScale = newValue;							// Set the value
			Debug.Log($"Timescale is now <color=#ffff00>{Time.timeScale}</color>");
		}

		private void FixedUpdate()
		{
			if (heightBar == null)
				return;
			
			if (Input.GetMouseButtonDown(1))
			{
				lastMousePosition = Input.mousePosition;
			}
			if (Input.GetMouseButton(1))
			{
				//Debug.Log("Value 1: " + heightBar.value);
				heightBar.value += (Input.mousePosition.y - lastMousePosition.y) * mouseSensitivityY;
				lastMousePosition = Input.mousePosition;
				//Debug.Log("Value 2: " + heightBar.value);
			}
		}
	}
}
