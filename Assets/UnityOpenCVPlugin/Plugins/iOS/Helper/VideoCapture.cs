using UnityEngine;
using System;
using System.Collections;
using System.Runtime.InteropServices;
using UnityEngine.XR.iOS;
using UnityEngine.UI;

public class VideoCapture : MonoBehaviour {
	// object image dimension
	int width;
	int height;

	// input width
	int inputWidth;
	int inputHeight;

	public Image renderTarget;
	public Texture2D objectTexture;
	public Text xText;
	public Text yText;
	public GameObject model;
	public LayerMask collisionLayer;
	public bool fixLocation;

	// only allow once for object descriptor detection
	bool detect = true;
	static int validationNum = 5;
	Vector2Int[] locations = new Vector2Int[validationNum];
	bool stopDetection = false;
	int counter = 0;


	// Import external library
	[DllImport("__Internal")]
	private static extern IntPtr allocateVideoCapture(int width, int height);

	[DllImport("__Internal")]
	private static extern void releaseVideoCapture(IntPtr capture);

	[DllImport("__Internal")]
	private static extern void updateVideoCapture(IntPtr capture, int width, int height, IntPtr inputImage, IntPtr outputImage);

	[DllImport("__Internal")]
	private static extern void OpenCVPixelData (int enable, IntPtr YPixelBytes);

	[DllImport("__Internal")]
	private static extern void imageTrigger (IntPtr capture, int inputWidth, int inputHeight, IntPtr inputScene, IntPtr inputObject, IntPtr x, IntPtr y, bool redetect, IntPtr outputImage);

	// Set up ARKit related parameters
	private UnityARSessionNativeInterface m_Session;
	private bool texturesInitialized;

	// Pointer to device capture object
	private IntPtr nativeCapture;

	// Video texture
	private byte[] textureYBytes;
	private GCHandle textureYHandle;
	private IntPtr textureYInputPtr;

	// Input object image
	private GCHandle objectImageHandle;
	private IntPtr objectImagePtr;

	// Output render image
	private Texture2D texture;
	private Color32[] pixels;
	private GCHandle pixelsHandle;
	private IntPtr pixelsOutputPtr;

	// Output x and y
	private int x;
	private GCHandle xHandle;
	private IntPtr xPtr;
	private int y;
	private GCHandle yHandle;
	private IntPtr yPtr;

	// Use this for initialization
	void Start () {

		model.SetActive (false);

		#if UNITY_IOS
		UnityARSessionNativeInterface.ARFrameUpdatedEvent += UpdateCamera;
		texturesInitialized = false;

		width = objectTexture.width;
		height = objectTexture.height;

		nativeCapture = allocateVideoCapture(width, height);

		texture = new Texture2D(640, 360, TextureFormat.ARGB32, false);
		pixels = texture.GetPixels32();
		pixelsHandle = GCHandle.Alloc(pixels, GCHandleType.Pinned);
		pixelsOutputPtr = pixelsHandle.AddrOfPinnedObject();

		objectImageHandle = GCHandle.Alloc (objectTexture.GetPixels32(), GCHandleType.Pinned);
		objectImagePtr = objectImageHandle.AddrOfPinnedObject ();

		xHandle = GCHandle.Alloc(x,GCHandleType.Pinned);
		xPtr = xHandle.AddrOfPinnedObject();
		yHandle = GCHandle.Alloc(y,GCHandleType.Pinned);
		yPtr = yHandle.AddrOfPinnedObject();

		#endif

		renderTarget.material.mainTexture = texture;

	}


	void UpdateCamera(UnityARCamera camera)
	{
		if (!texturesInitialized) {
			InitializeTextures (camera);
		}
		UnityARSessionNativeInterface.ARFrameUpdatedEvent -= UpdateCamera;

	}


	void InitializeTextures(UnityARCamera camera)
	{
		inputWidth = camera.videoParams.yWidth;
		inputHeight = camera.videoParams.yHeight;

		textureYBytes = new byte[inputWidth * inputHeight];
		textureYHandle = GCHandle.Alloc(textureYBytes, GCHandleType.Pinned);
		textureYInputPtr = textureYHandle.AddrOfPinnedObject();

		texturesInitialized = true;
	}


	void Update() {
		
		#if UNITY_IOS

		if (!texturesInitialized)
			return;

		if (stopDetection)
			return;

		//Fetch the video texture
		SetOpenCVPixelData (true, textureYInputPtr);

		//Display video
//		updateVideoCapture(nativeCapture, inputWidth, inputHeight, textureYInputPtr, pixelsOutputPtr);
//		texture.SetPixels32(pixels);
//		texture.Apply();

		imageTrigger(nativeCapture, inputWidth, inputHeight, textureYInputPtr, objectImagePtr, xPtr, yPtr, detect, pixelsOutputPtr);
		texture.SetPixels32(pixels);
		texture.Apply();

		x = Marshal.ReadInt32(xPtr);
		y = Marshal.ReadInt32(yPtr);

		xText.text = "x = " + x.ToString();
		yText.text = "y = " + y.ToString();


        if (fixLocation && x != 0)
        {
            locations[counter].x = x;
            locations[counter].y = y;
            counter = counter + 1;

            if (counter == validationNum)
                counter = 0;

            float variance = Variance(locations, validationNum);
            // Debug.Log(variance);

            if (variance <= 3f)
            {
                Ray ray = Camera.main.ScreenPointToRay(new Vector2(x * 2, y * 2));
                RaycastHit hit;

                if (Physics.Raycast(ray, out hit, 100, collisionLayer))
                {
                    model.transform.position = hit.point;
                    if (!model.activeInHierarchy)
                    {
                        model.SetActive(true);
                    }

                    stopDetection = true;

                }

            }

		}

		detect = false;

		#endif

	}

	void OnDestroy () {
		
		#if UNITY_IOS

		textureYHandle.Free();
		objectImageHandle.Free();
		pixelsHandle.Free();
		xHandle.Free();
		yHandle.Free();
		releaseVideoCapture(nativeCapture);

		#endif
	}


	public void SetOpenCVPixelData(bool enable, IntPtr YByteArray)
	{
		#if UNITY_IOS

		int iEnable = enable ? 1 : 0;
		OpenCVPixelData (iEnable, YByteArray);

		#endif
	}

	public float Variance(Vector2Int[] points, int num)
	{
		int sum_x = 0;
		int sum_y = 0;
		int average_x = 0;
		int average_y = 0;
		float variance_square = 0;

		for (int i = 0; i < num; i++) {
			sum_x += points [i].x;
			sum_y += points [i].y;
		}

		average_x = sum_x / num;
		average_y = sum_y / num;

		Vector2 midPoint = new Vector2 (average_x, average_y);

		for (int i = 0; i < num; i++) {
			variance_square += Vector2.Distance (points [i], midPoint);
		}

		return Mathf.Sqrt (variance_square);
	}

	public void SetStopDetection()
	{
		stopDetection = true;
	}
}
