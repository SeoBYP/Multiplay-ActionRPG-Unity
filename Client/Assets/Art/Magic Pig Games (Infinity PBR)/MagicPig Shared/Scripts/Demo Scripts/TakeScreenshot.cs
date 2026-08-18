using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

namespace InfinityPBR.Demo
{
	public class TakeScreenshot : MonoBehaviour
	{
		[Header("Options")]
		public KeyCode takeScreenshot = KeyCode.Y;
		public string prefix = "Screenshot";
		public int sizeMultiplier = 3;
		
		void Update()
		{
			#if UNITY_EDITOR
			if (Input.GetKeyDown(takeScreenshot)) TakeScreenshotNow();
			#endif
		}
		
		private void TakeScreenshotNow(){
#if UNITY_EDITOR
			if (!EditorPrefs.HasKey("ScreenshotCount"))
				EditorPrefs.SetInt("ScreenshotCount", 0);

			ScreenCapture.CaptureScreenshot($"{prefix}_{EditorPrefs.GetInt("ScreenshotCount")}.png", sizeMultiplier);
			EditorPrefs.SetInt("ScreenshotCount", EditorPrefs.GetInt("ScreenshotCount") + 1);
			#endif
		}
	}
}
