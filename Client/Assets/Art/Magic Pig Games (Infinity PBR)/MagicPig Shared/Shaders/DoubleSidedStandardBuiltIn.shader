Shader "Infinity PBR/DoubleSidedStandardBuiltIn"
{
    Properties
    {
        _Color ("Color", Color) = (1,1,1,1)
        _MainTex ("Albedo (RGB)", 2D) = "white" {}
        _MetallicGlossMap ("Metallic (R) Gloss (A)", 2D) = "white" {}
        _NormalMap ("Normal Map", 2D) = "bump" {}
    }
    SubShader
    {
        // Turn off culling to render both front and back faces
        Cull Off

        Tags { "RenderType"="Opaque" }
        LOD 200

        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows

        #pragma target 3.0

        sampler2D _MainTex;
        sampler2D _MetallicGlossMap;
        sampler2D _NormalMap;

        struct Input
        {
            float2 uv_MainTex;
        };

        fixed4 _Color;

        UNITY_INSTANCING_BUFFER_START(Props)
            // put more per-instance properties here
        UNITY_INSTANCING_BUFFER_END(Props)

        void surf (Input IN, inout SurfaceOutputStandard o)
        {
            // Albedo comes from a texture tinted by color
            fixed4 c = tex2D (_MainTex, IN.uv_MainTex) * _Color;
            // Sample the metallic and gloss map
            fixed4 metallicGloss = tex2D(_MetallicGlossMap, IN.uv_MainTex);
            // Sample the normal map
            fixed4 bump = tex2D(_NormalMap, IN.uv_MainTex);

            o.Albedo = c.rgb;
            // Metallic from the R channel
            o.Metallic = metallicGloss.r;
            // Smoothness from the A channel
            o.Smoothness = metallicGloss.a;
            // Normal from the normal map
            o.Normal = UnpackNormal(bump);
            o.Alpha = c.a;
        }
        ENDCG
    }
    FallBack "Diffuse"
}
