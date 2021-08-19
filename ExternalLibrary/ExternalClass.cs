using UnityEngine;

namespace ExternalLibrary
{
    public static class ExternalClass
    {
	    public static int Add(int a, int b)
	    {
		    return a * b;
	    }

		public static void UnityLog(string msg)
        {
            Debug.Log("Hello from external: " + msg);
        }
    }
}
