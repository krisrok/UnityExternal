using ExternalLibrary;
using UnityEngine;

public class TestBehaviourScript : MonoBehaviour
{

	private void Reset()
	{
		int result = ExternalClass.Add(40, 2);
		Debug.Log("Result: " + result);
		ExternalClass.UnityLog("YOOHOO");
	}
}
