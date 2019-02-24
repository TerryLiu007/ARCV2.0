Shader "Unlit/DepthMask"
{
	SubShader
	{
		Tags {"Queue"="Geometry-1"}

		ZWrite on
		ZTest LEqual
		ColorMask 0

		Pass
		{

		}
	}
}
