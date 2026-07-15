// Shader created with Shader Forge v1.38 
// Shader Forge (c) Neat Corporation / Joachim Holmer - http://www.acegikmo.com/shaderforge/
// Note: Manually altering this data may prevent you from opening it in Shader Forge
/*SF_DATA;ver:1.38;sub:START;pass:START;ps:flbk:,iptp:0,cusa:False,bamd:0,cgin:,lico:1,lgpr:1,limd:3,spmd:1,trmd:0,grmd:0,uamb:True,mssp:True,bkdf:True,hqlp:True,rprd:True,enco:False,rmgx:True,imps:True,rpth:1,vtps:0,hqsc:True,nrmq:1,nrsp:0,vomd:0,spxs:False,tesm:0,olmd:1,culm:0,bsrc:0,bdst:1,dpts:2,wrdp:True,dith:0,atcv:False,rfrpo:True,rfrpn:Refraction,coma:15,ufog:True,aust:True,igpj:False,qofs:0,qpre:1,rntp:1,fgom:False,fgoc:False,fgod:False,fgor:False,fgmd:0,fgcr:0.5,fgcg:0.5,fgcb:0.5,fgca:1,fgde:0.01,fgrn:0,fgrf:300,stcl:False,atwp:False,stva:128,stmr:255,stmw:255,stcp:6,stps:0,stfa:0,stfz:0,ofsf:0,ofsu:0,f2p0:False,fnsp:False,fnfb:False,fsmp:False;n:type:ShaderForge.SFN_Final,id:1,x:33396,y:33252,varname:node_1,prsc:2|diff-66-OUT,spec-38-OUT,gloss-46-OUT,normal-56-OUT,emission-73-OUT,lwrap-92-OUT,amdfl-148-OUT,disp-110-OUT,tess-115-OUT;n:type:ShaderForge.SFN_Tex2d,id:2,x:32243,y:32586,ptovrint:False,ptlb:Base,ptin:_Base,varname:node_7683,prsc:2,glob:False,taghide:False,taghdr:False,tagprd:False,tagnsco:False,tagnrm:False,ntxv:0,isnm:False;n:type:ShaderForge.SFN_Tex2d,id:3,x:33206,y:32770,ptovrint:False,ptlb:Ambient Oclussion,ptin:_AmbientOclussion,varname:node_7376,prsc:2,glob:False,taghide:False,taghdr:False,tagprd:False,tagnsco:False,tagnrm:False,ntxv:0,isnm:False|MIP-260-OUT;n:type:ShaderForge.SFN_Tex2d,id:5,x:31570,y:32885,ptovrint:False,ptlb:Normal,ptin:_Normal,varname:node_3749,prsc:2,glob:False,taghide:False,taghdr:False,tagprd:False,tagnsco:False,tagnrm:False,ntxv:3,isnm:True;n:type:ShaderForge.SFN_Tex2d,id:7,x:32058,y:32811,ptovrint:False,ptlb:Specular,ptin:_Specular,varname:node_5921,prsc:2,glob:False,taghide:False,taghdr:False,tagprd:False,tagnsco:False,tagnrm:False,ntxv:0,isnm:False;n:type:ShaderForge.SFN_Tex2d,id:9,x:32058,y:32995,ptovrint:False,ptlb:Gloss,ptin:_Gloss,varname:node_2385,prsc:2,glob:False,taghide:False,taghdr:False,tagprd:False,tagnsco:False,tagnrm:False,ntxv:0,isnm:False;n:type:ShaderForge.SFN_Tex2d,id:11,x:32240,y:33311,ptovrint:False,ptlb:Reflection Mask,ptin:_ReflectionMask,varname:node_1587,prsc:2,glob:False,taghide:False,taghdr:False,tagprd:False,tagnsco:False,tagnrm:False,ntxv:0,isnm:False;n:type:ShaderForge.SFN_Tex2d,id:13,x:32433,y:33621,ptovrint:False,ptlb:Trans Mask,ptin:_TransMask,varname:node_2194,prsc:2,glob:False,taghide:False,taghdr:False,tagprd:False,tagnsco:False,tagnrm:False,tex:72f052122502345f1b365b37455a8e3d,ntxv:0,isnm:False;n:type:ShaderForge.SFN_Tex2d,id:15,x:32745,y:33866,ptovrint:False,ptlb:Displace,ptin:_Displace,varname:node_8913,prsc:2,glob:False,taghide:False,taghdr:False,tagprd:False,tagnsco:False,tagnrm:False,ntxv:0,isnm:False;n:type:ShaderForge.SFN_Cubemap,id:16,x:32034,y:33318,ptovrint:False,ptlb:Reflection Map,ptin:_ReflectionMap,varname:node_7720,prsc:2,glob:False,taghide:False,taghdr:False,tagprd:False,tagnsco:False,tagnrm:False,pvfc:0;n:type:ShaderForge.SFN_Lerp,id:17,x:33441,y:32742,varname:node_17,prsc:2|A-3-A,B-20-OUT,T-21-OUT;n:type:ShaderForge.SFN_Vector1,id:18,x:32804,y:32641,varname:node_18,prsc:2,v1:0;n:type:ShaderForge.SFN_Vector1,id:20,x:32804,y:32588,varname:node_20,prsc:2,v1:1;n:type:ShaderForge.SFN_ValueProperty,id:21,x:33206,y:32960,ptovrint:False,ptlb:AO Burn,ptin:_AOBurn,varname:node_6252,prsc:2,glob:False,taghide:False,taghdr:False,tagprd:False,tagnsco:False,tagnrm:False,v1:0;n:type:ShaderForge.SFN_ValueProperty,id:22,x:33206,y:33057,ptovrint:False,ptlb:AO Level,ptin:_AOLevel,varname:node_7804,prsc:2,glob:False,taghide:False,taghdr:False,tagprd:False,tagnsco:False,tagnrm:False,v1:0.5;n:type:ShaderForge.SFN_SwitchProperty,id:24,x:33206,y:32608,ptovrint:False,ptlb:AO Enable,ptin:_AOEnable,varname:node_7957,prsc:2,glob:False,taghide:False,taghdr:False,tagprd:False,tagnsco:False,tagnrm:False,on:True|A-20-OUT,B-220-OUT;n:type:ShaderForge.SFN_Multiply,id:25,x:32401,y:32603,varname:node_25,prsc:2|A-2-RGB,B-27-OUT;n:type:ShaderForge.SFN_Tex2d,id:26,x:32243,y:32392,ptovrint:False,ptlb:Mask Map,ptin:_MaskMap,varname:node_1032,prsc:2,glob:False,taghide:False,taghdr:False,tagprd:False,tagnsco:False,tagnrm:False,ntxv:0,isnm:False;n:type:ShaderForge.SFN_OneMinus,id:27,x:32414,y:32392,varname:node_27,prsc:2|IN-26-A;n:type:ShaderForge.SFN_Color,id:29,x:32438,y:32213,ptovrint:False,ptlb:Color Tint,ptin:_ColorTint,varname:node_4976,prsc:2,glob:False,taghide:False,taghdr:False,tagprd:False,tagnsco:False,tagnrm:False,c1:0.02205884,c2:1,c3:0.5808825,c4:1;n:type:ShaderForge.SFN_Multiply,id:30,x:32611,y:32350,varname:node_30,prsc:2|A-29-RGB,B-26-A;n:type:ShaderForge.SFN_Add,id:31,x:32574,y:32541,varname:node_31,prsc:2|A-30-OUT,B-25-OUT;n:type:ShaderForge.SFN_Multiply,id:32,x:32574,y:32691,varname:node_32,prsc:2|A-24-OUT,B-168-OUT;n:type:ShaderForge.SFN_Multiply,id:38,x:32274,y:32811,varname:node_38,prsc:2|A-7-A,B-39-OUT,C-24-OUT,D-176-RGB;n:type:ShaderForge.SFN_ValueProperty,id:39,x:32274,y:32950,ptovrint:False,ptlb:Specular Level,ptin:_SpecularLevel,varname:node_7899,prsc:2,glob:False,taghide:False,taghdr:False,tagprd:False,tagnsco:False,tagnrm:False,v1:1;n:type:ShaderForge.SFN_Multiply,id:46,x:32497,y:33019,varname:node_46,prsc:2|A-47-OUT,B-48-OUT;n:type:ShaderForge.SFN_SwitchProperty,id:47,x:32364,y:33155,ptovrint:False,ptlb:Custom Gloss,ptin:_CustomGloss,varname:node_9633,prsc:2,glob:False,taghide:False,taghdr:False,tagprd:False,tagnsco:False,tagnrm:False,on:False|A-20-OUT,B-9-A;n:type:ShaderForge.SFN_Slider,id:48,x:32195,y:33039,ptovrint:False,ptlb:Shinniness,ptin:_Shinniness,varname:node_5510,prsc:2,glob:False,taghide:False,taghdr:False,tagprd:False,tagnsco:False,tagnrm:False,min:0,cur:0.5,max:1;n:type:ShaderForge.SFN_Vector3,id:54,x:31570,y:33057,varname:node_54,prsc:2,v1:0,v2:0,v3:1;n:type:ShaderForge.SFN_ValueProperty,id:55,x:31570,y:33177,ptovrint:False,ptlb:Normal Smooth,ptin:_NormalSmooth,varname:node_3962,prsc:2,glob:False,taghide:False,taghdr:False,tagprd:False,tagnsco:False,tagnrm:False,v1:0;n:type:ShaderForge.SFN_Lerp,id:56,x:31781,y:33029,varname:node_56,prsc:2|A-5-RGB,B-54-OUT,T-55-OUT;n:type:ShaderForge.SFN_Multiply,id:62,x:32422,y:33311,varname:node_62,prsc:2|A-16-RGB,B-11-A;n:type:ShaderForge.SFN_Color,id:63,x:32539,y:33191,ptovrint:False,ptlb:Reflection Tint,ptin:_ReflectionTint,varname:node_917,prsc:2,glob:False,taghide:False,taghdr:False,tagprd:False,tagnsco:False,tagnrm:False,c1:1,c2:1,c3:1,c4:1;n:type:ShaderForge.SFN_ValueProperty,id:64,x:32539,y:33393,ptovrint:False,ptlb:Reflection Power,ptin:_ReflectionPower,varname:node_1012,prsc:2,glob:False,taghide:False,taghdr:False,tagprd:False,tagnsco:False,tagnrm:False,v1:1;n:type:ShaderForge.SFN_Multiply,id:65,x:32696,y:33191,varname:node_65,prsc:2|A-63-RGB,B-62-OUT,C-64-OUT;n:type:ShaderForge.SFN_Blend,id:66,x:32769,y:32892,varname:node_66,prsc:2,blmd:5,clmp:True|SRC-32-OUT,DST-65-OUT;n:type:ShaderForge.SFN_ValueProperty,id:72,x:32680,y:33393,ptovrint:False,ptlb:Reflection Emission,ptin:_ReflectionEmission,varname:node_5083,prsc:2,glob:False,taghide:False,taghdr:False,tagprd:False,tagnsco:False,tagnrm:False,v1:0;n:type:ShaderForge.SFN_Multiply,id:73,x:32843,y:33271,varname:node_73,prsc:2|A-65-OUT,B-72-OUT;n:type:ShaderForge.SFN_ValueProperty,id:80,x:32433,y:33804,ptovrint:False,ptlb:Trans Power,ptin:_TransPower,varname:node_9468,prsc:2,glob:False,taghide:False,taghdr:False,tagprd:False,tagnsco:False,tagnrm:False,v1:1;n:type:ShaderForge.SFN_Multiply,id:81,x:32644,y:33614,varname:node_81,prsc:2|A-13-A,B-80-OUT,C-285-RGB;n:type:ShaderForge.SFN_SwitchProperty,id:92,x:32833,y:33566,ptovrint:False,ptlb:Translucency,ptin:_Translucency,varname:node_6229,prsc:2,glob:False,taghide:False,taghdr:False,tagprd:False,tagnsco:False,tagnrm:False,on:False|A-18-OUT,B-81-OUT;n:type:ShaderForge.SFN_Lerp,id:108,x:32926,y:33957,varname:node_108,prsc:2|A-20-OUT,B-15-A,T-109-OUT;n:type:ShaderForge.SFN_ValueProperty,id:109,x:32745,y:34067,ptovrint:False,ptlb:Disp Burn,ptin:_DispBurn,varname:node_9992,prsc:2,glob:False,taghide:False,taghdr:False,tagprd:False,tagnsco:False,tagnrm:False,v1:1;n:type:ShaderForge.SFN_Multiply,id:110,x:33168,y:33710,varname:node_110,prsc:2|A-113-OUT,B-112-OUT,C-121-OUT;n:type:ShaderForge.SFN_Max,id:111,x:33119,y:33898,varname:node_111,prsc:2|A-18-OUT,B-108-OUT;n:type:ShaderForge.SFN_Min,id:112,x:33338,y:33898,varname:node_112,prsc:2|A-20-OUT,B-111-OUT;n:type:ShaderForge.SFN_ValueProperty,id:113,x:32745,y:33784,ptovrint:False,ptlb:Disp Power,ptin:_DispPower,varname:node_1156,prsc:2,glob:False,taghide:False,taghdr:False,tagprd:False,tagnsco:False,tagnrm:False,v1:1;n:type:ShaderForge.SFN_ValueProperty,id:115,x:33364,y:33791,ptovrint:False,ptlb:Tesellation,ptin:_Tesellation,varname:node_5672,prsc:2,glob:False,taghide:False,taghdr:False,tagprd:False,tagnsco:False,tagnrm:False,v1:1;n:type:ShaderForge.SFN_NormalVector,id:121,x:32916,y:33740,prsc:2,pt:False;n:type:ShaderForge.SFN_Multiply,id:137,x:33145,y:34452,varname:node_137,prsc:2|A-138-OUT,B-140-OUT,C-141-RGB;n:type:ShaderForge.SFN_Fresnel,id:138,x:32875,y:34331,varname:node_138,prsc:2|EXP-139-OUT;n:type:ShaderForge.SFN_ValueProperty,id:139,x:32699,y:34349,ptovrint:False,ptlb:Rim Fresnel,ptin:_RimFresnel,varname:node_1060,prsc:2,glob:False,taghide:False,taghdr:False,tagprd:False,tagnsco:False,tagnrm:False,v1:3;n:type:ShaderForge.SFN_ValueProperty,id:140,x:32875,y:34486,ptovrint:False,ptlb:Rim Power,ptin:_RimPower,varname:node_6982,prsc:2,glob:False,taghide:False,taghdr:False,tagprd:False,tagnsco:False,tagnrm:False,v1:1;n:type:ShaderForge.SFN_Color,id:141,x:32875,y:34578,ptovrint:False,ptlb:Rim Color,ptin:_RimColor,varname:node_6250,prsc:2,glob:False,taghide:False,taghdr:False,tagprd:False,tagnsco:False,tagnrm:False,c1:0.9264706,c2:0.5858564,c3:0.5858564,c4:1;n:type:ShaderForge.SFN_Tex2d,id:142,x:32875,y:34761,ptovrint:False,ptlb:Rim Mask,ptin:_RimMask,varname:node_1530,prsc:2,glob:False,taghide:False,taghdr:False,tagprd:False,tagnsco:False,tagnrm:False,ntxv:0,isnm:False;n:type:ShaderForge.SFN_Blend,id:143,x:33145,y:34604,varname:node_143,prsc:2,blmd:0,clmp:True|SRC-145-OUT,DST-142-A;n:type:ShaderForge.SFN_Max,id:145,x:33321,y:34452,varname:node_145,prsc:2|A-18-OUT,B-137-OUT;n:type:ShaderForge.SFN_Multiply,id:146,x:33191,y:34836,varname:node_146,prsc:2|A-149-RGB,B-150-OUT;n:type:ShaderForge.SFN_Add,id:148,x:33474,y:34706,varname:node_148,prsc:2|A-143-OUT,B-146-OUT;n:type:ShaderForge.SFN_AmbientLight,id:149,x:32856,y:34945,varname:node_149,prsc:2;n:type:ShaderForge.SFN_ValueProperty,id:150,x:32856,y:35100,ptovrint:False,ptlb:Ambiental Power,ptin:_AmbientalPower,varname:node_5434,prsc:2,glob:False,taghide:False,taghdr:False,tagprd:False,tagnsco:False,tagnrm:False,v1:1;n:type:ShaderForge.SFN_SwitchProperty,id:168,x:32773,y:32774,ptovrint:False,ptlb:Color Mask,ptin:_ColorMask,varname:node_159,prsc:2,glob:False,taghide:False,taghdr:False,tagprd:False,tagnsco:False,tagnrm:False,on:False|A-2-RGB,B-31-OUT;n:type:ShaderForge.SFN_Color,id:176,x:32444,y:32852,ptovrint:False,ptlb:Spec Color,ptin:_SpecColor,varname:node_454,prsc:2,glob:False,taghide:False,taghdr:False,tagprd:False,tagnsco:False,tagnrm:False,c1:0.5,c2:0.5,c3:0.5,c4:1;n:type:ShaderForge.SFN_Min,id:197,x:33601,y:32899,varname:node_197,prsc:2|A-243-OUT,B-20-OUT;n:type:ShaderForge.SFN_Max,id:220,x:33801,y:32899,varname:node_220,prsc:2|A-197-OUT,B-18-OUT;n:type:ShaderForge.SFN_Blend,id:243,x:33418,y:32960,varname:node_243,prsc:2,blmd:10,clmp:True|SRC-17-OUT,DST-22-OUT;n:type:ShaderForge.SFN_Slider,id:260,x:33164,y:33157,ptovrint:False,ptlb:AO Detail,ptin:_AODetail,varname:node_546,prsc:2,glob:False,taghide:False,taghdr:False,tagprd:False,tagnsco:False,tagnrm:False,min:0,cur:0,max:10;n:type:ShaderForge.SFN_Color,id:285,x:32594,y:33477,ptovrint:False,ptlb:Trans Color,ptin:_TransColor,varname:node_4485,prsc:2,glob:False,taghide:False,taghdr:False,tagprd:False,tagnsco:False,tagnrm:False,c1:0.5,c2:0.5,c3:0.5,c4:1;proporder:2-168-29-26-176-39-7-48-47-9-5-55-24-3-21-22-260-63-64-16-11-72-92-80-285-13-115-113-109-15-141-140-139-142-150;pass:END;sub:END;*/

Shader "DLNK/DX11TSShader" {
    Properties {
        _Base ("Base", 2D) = "white" {}
        [MaterialToggle] _ColorMask ("Color Mask", Float ) = 0
        _ColorTint ("Color Tint", Color) = (0.02205884,1,0.5808825,1)
        _MaskMap ("Mask Map", 2D) = "white" {}
        _SpecColor ("Spec Color", Color) = (0.5,0.5,0.5,1)
        _SpecularLevel ("Specular Level", Float ) = 1
        _Specular ("Specular", 2D) = "white" {}
        _Shinniness ("Shinniness", Range(0, 1)) = 0.5
        [MaterialToggle] _CustomGloss ("Custom Gloss", Float ) = 1
        _Gloss ("Gloss", 2D) = "white" {}
        _Normal ("Normal", 2D) = "bump" {}
        _NormalSmooth ("Normal Smooth", Float ) = 0
        [MaterialToggle] _AOEnable ("AO Enable", Float ) = 1
        _AmbientOclussion ("Ambient Oclussion", 2D) = "white" {}
        _AOBurn ("AO Burn", Float ) = 0
        _AOLevel ("AO Level", Float ) = 0.5
        _AODetail ("AO Detail", Range(0, 10)) = 0
        _ReflectionTint ("Reflection Tint", Color) = (1,1,1,1)
        _ReflectionPower ("Reflection Power", Float ) = 1
        _ReflectionMap ("Reflection Map", Cube) = "_Skybox" {}
        _ReflectionMask ("Reflection Mask", 2D) = "white" {}
        _ReflectionEmission ("Reflection Emission", Float ) = 0
        [MaterialToggle] _Translucency ("Translucency", Float ) = 0
        _TransPower ("Trans Power", Float ) = 1
        _TransColor ("Trans Color", Color) = (0.5,0.5,0.5,1)
        _TransMask ("Trans Mask", 2D) = "white" {}
        _Tesellation ("Tesellation", Float ) = 1
        _DispPower ("Disp Power", Float ) = 1
        _DispBurn ("Disp Burn", Float ) = 1
        _Displace ("Displace", 2D) = "white" {}
        _RimColor ("Rim Color", Color) = (0.9264706,0.5858564,0.5858564,1)
        _RimPower ("Rim Power", Float ) = 1
        _RimFresnel ("Rim Fresnel", Float ) = 3
        _RimMask ("Rim Mask", 2D) = "white" {}
        _AmbientalPower ("Ambiental Power", Float ) = 1
    }
    SubShader {
        Tags {
            "RenderType"="Opaque"
        }
        Pass {
            Name "DEFERRED"
            Tags {
                "LightMode"="Deferred"
            }
            
            
            CGPROGRAM
            #pragma hull hull
            #pragma domain domain
            #pragma vertex tessvert
            #pragma fragment frag
            #define UNITY_PASS_DEFERRED
            #define SHOULD_SAMPLE_SH ( defined (LIGHTMAP_OFF) && defined(DYNAMICLIGHTMAP_OFF) )
            #define _GLOSSYENV 1
            #include "UnityCG.cginc"
            #include "Lighting.cginc"
            #include "Tessellation.cginc"
            #include "UnityPBSLighting.cginc"
            #include "UnityStandardBRDF.cginc"
            #pragma fragmentoption ARB_precision_hint_fastest
            #pragma multi_compile_shadowcaster
            #pragma multi_compile ___ UNITY_HDR_ON
            #pragma multi_compile LIGHTMAP_OFF LIGHTMAP_ON
            #pragma multi_compile DIRLIGHTMAP_OFF DIRLIGHTMAP_COMBINED DIRLIGHTMAP_SEPARATE
            #pragma multi_compile DYNAMICLIGHTMAP_OFF DYNAMICLIGHTMAP_ON
            #pragma multi_compile_fog
            #pragma only_renderers d3d9 d3d11 glcore gles gles3 metal xboxone ps4 psp2 n3ds wiiu 
            #pragma target 5.0
            uniform sampler2D _Base; uniform float4 _Base_ST;
            uniform sampler2D _AmbientOclussion; uniform float4 _AmbientOclussion_ST;
            uniform sampler2D _Normal; uniform float4 _Normal_ST;
            uniform sampler2D _Specular; uniform float4 _Specular_ST;
            uniform sampler2D _Gloss; uniform float4 _Gloss_ST;
            uniform sampler2D _ReflectionMask; uniform float4 _ReflectionMask_ST;
            uniform sampler2D _TransMask; uniform float4 _TransMask_ST;
            uniform sampler2D _Displace; uniform float4 _Displace_ST;
            uniform samplerCUBE _ReflectionMap;
            uniform float _AOBurn;
            uniform float _AOLevel;
            uniform fixed _AOEnable;
            uniform sampler2D _MaskMap; uniform float4 _MaskMap_ST;
            uniform float4 _ColorTint;
            uniform float _SpecularLevel;
            uniform fixed _CustomGloss;
            uniform float _Shinniness;
            uniform float _NormalSmooth;
            uniform float4 _ReflectionTint;
            uniform float _ReflectionPower;
            uniform float _ReflectionEmission;
            uniform float _TransPower;
            uniform fixed _Translucency;
            uniform float _DispBurn;
            uniform float _DispPower;
            uniform float _Tesellation;
            uniform float _RimFresnel;
            uniform float _RimPower;
            uniform float4 _RimColor;
            uniform sampler2D _RimMask; uniform float4 _RimMask_ST;
            uniform float _AmbientalPower;
            uniform fixed _ColorMask;
            uniform float _AODetail;
            uniform float4 _TransColor;
            struct VertexInput {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                float4 tangent : TANGENT;
                float2 texcoord0 : TEXCOORD0;
                float2 texcoord1 : TEXCOORD1;
                float2 texcoord2 : TEXCOORD2;
            };
            struct VertexOutput {
                float4 pos : SV_POSITION;
                float2 uv0 : TEXCOORD0;
                float2 uv1 : TEXCOORD1;
                float2 uv2 : TEXCOORD2;
                float4 posWorld : TEXCOORD3;
                float3 normalDir : TEXCOORD4;
                float3 tangentDir : TEXCOORD5;
                float3 bitangentDir : TEXCOORD6;
                #if defined(LIGHTMAP_ON) || defined(UNITY_SHOULD_SAMPLE_SH)
                    float4 ambientOrLightmapUV : TEXCOORD7;
                #endif
            };
            VertexOutput vert (VertexInput v) {
                VertexOutput o = (VertexOutput)0;
                o.uv0 = v.texcoord0;
                o.uv1 = v.texcoord1;
                o.uv2 = v.texcoord2;
                #ifdef LIGHTMAP_ON
                    o.ambientOrLightmapUV.xy = v.texcoord1.xy * unity_LightmapST.xy + unity_LightmapST.zw;
                    o.ambientOrLightmapUV.zw = 0;
                #endif
                #ifdef DYNAMICLIGHTMAP_ON
                    o.ambientOrLightmapUV.zw = v.texcoord2.xy * unity_DynamicLightmapST.xy + unity_DynamicLightmapST.zw;
                #endif
                o.normalDir = UnityObjectToWorldNormal(v.normal);
                o.tangentDir = normalize( mul( unity_ObjectToWorld, float4( v.tangent.xyz, 0.0 ) ).xyz );
                o.bitangentDir = normalize(cross(o.normalDir, o.tangentDir) * v.tangent.w);
                o.posWorld = mul(unity_ObjectToWorld, v.vertex);
                o.pos = UnityObjectToClipPos( v.vertex );
                return o;
            }
            #ifdef UNITY_CAN_COMPILE_TESSELLATION
                struct TessVertex {
                    float4 vertex : INTERNALTESSPOS;
                    float3 normal : NORMAL;
                    float4 tangent : TANGENT;
                    float2 texcoord0 : TEXCOORD0;
                    float2 texcoord1 : TEXCOORD1;
                    float2 texcoord2 : TEXCOORD2;
                };
                struct OutputPatchConstant {
                    float edge[3]         : SV_TessFactor;
                    float inside          : SV_InsideTessFactor;
                    float3 vTangent[4]    : TANGENT;
                    float2 vUV[4]         : TEXCOORD;
                    float3 vTanUCorner[4] : TANUCORNER;
                    float3 vTanVCorner[4] : TANVCORNER;
                    float4 vCWts          : TANWEIGHTS;
                };
                TessVertex tessvert (VertexInput v) {
                    TessVertex o;
                    o.vertex = v.vertex;
                    o.normal = v.normal;
                    o.tangent = v.tangent;
                    o.texcoord0 = v.texcoord0;
                    o.texcoord1 = v.texcoord1;
                    o.texcoord2 = v.texcoord2;
                    return o;
                }
                void displacement (inout VertexInput v){
                    float node_20 = 1.0;
                    float node_18 = 0.0;
                    float4 _Displace_var = tex2Dlod(_Displace,float4(TRANSFORM_TEX(v.texcoord0, _Displace),0.0,0));
                    v.vertex.xyz += (_DispPower*min(node_20,max(node_18,lerp(node_20,_Displace_var.a,_DispBurn)))*v.normal);
                }
                float Tessellation(TessVertex v){
                    return _Tesellation;
                }
                float4 Tessellation(TessVertex v, TessVertex v1, TessVertex v2){
                    float tv = Tessellation(v);
                    float tv1 = Tessellation(v1);
                    float tv2 = Tessellation(v2);
                    return float4( tv1+tv2, tv2+tv, tv+tv1, tv+tv1+tv2 ) / float4(2,2,2,3);
                }
                OutputPatchConstant hullconst (InputPatch<TessVertex,3> v) {
                    OutputPatchConstant o = (OutputPatchConstant)0;
                    float4 ts = Tessellation( v[0], v[1], v[2] );
                    o.edge[0] = ts.x;
                    o.edge[1] = ts.y;
                    o.edge[2] = ts.z;
                    o.inside = ts.w;
                    return o;
                }
                [domain("tri")]
                [partitioning("fractional_odd")]
                [outputtopology("triangle_cw")]
                [patchconstantfunc("hullconst")]
                [outputcontrolpoints(3)]
                TessVertex hull (InputPatch<TessVertex,3> v, uint id : SV_OutputControlPointID) {
                    return v[id];
                }
                [domain("tri")]
                VertexOutput domain (OutputPatchConstant tessFactors, const OutputPatch<TessVertex,3> vi, float3 bary : SV_DomainLocation) {
                    VertexInput v = (VertexInput)0;
                    v.vertex = vi[0].vertex*bary.x + vi[1].vertex*bary.y + vi[2].vertex*bary.z;
                    v.normal = vi[0].normal*bary.x + vi[1].normal*bary.y + vi[2].normal*bary.z;
                    v.tangent = vi[0].tangent*bary.x + vi[1].tangent*bary.y + vi[2].tangent*bary.z;
                    v.texcoord0 = vi[0].texcoord0*bary.x + vi[1].texcoord0*bary.y + vi[2].texcoord0*bary.z;
                    v.texcoord1 = vi[0].texcoord1*bary.x + vi[1].texcoord1*bary.y + vi[2].texcoord1*bary.z;
                    displacement(v);
                    VertexOutput o = vert(v);
                    return o;
                }
            #endif
            void frag(
                VertexOutput i,
                out half4 outDiffuse : SV_Target0,
                out half4 outSpecSmoothness : SV_Target1,
                out half4 outNormal : SV_Target2,
                out half4 outEmission : SV_Target3 )
            {
                i.normalDir = normalize(i.normalDir);
                float3x3 tangentTransform = float3x3( i.tangentDir, i.bitangentDir, i.normalDir);
                float3 viewDirection = normalize(_WorldSpaceCameraPos.xyz - i.posWorld.xyz);
                float3 _Normal_var = UnpackNormal(tex2D(_Normal,TRANSFORM_TEX(i.uv0, _Normal)));
                float3 normalLocal = lerp(_Normal_var.rgb,float3(0,0,1),_NormalSmooth);
                float3 normalDirection = normalize(mul( normalLocal, tangentTransform )); // Perturbed normals
                float3 viewReflectDirection = reflect( -viewDirection, normalDirection );
////// Lighting:
                float Pi = 3.141592654;
                float InvPi = 0.31830988618;
///////// Gloss:
                float node_20 = 1.0;
                float4 _Gloss_var = tex2D(_Gloss,TRANSFORM_TEX(i.uv0, _Gloss));
                float gloss = (lerp( node_20, _Gloss_var.a, _CustomGloss )*_Shinniness);
                float perceptualRoughness = 1.0 - (lerp( node_20, _Gloss_var.a, _CustomGloss )*_Shinniness);
                float roughness = perceptualRoughness * perceptualRoughness;
/////// GI Data:
                UnityLight light; // Dummy light
                light.color = 0;
                light.dir = half3(0,1,0);
                light.ndotl = max(0,dot(normalDirection,light.dir));
                UnityGIInput d;
                d.light = light;
                d.worldPos = i.posWorld.xyz;
                d.worldViewDir = viewDirection;
                d.atten = 1;
                #if defined(LIGHTMAP_ON) || defined(DYNAMICLIGHTMAP_ON)
                    d.ambient = 0;
                    d.lightmapUV = i.ambientOrLightmapUV;
                #else
                    d.ambient = i.ambientOrLightmapUV;
                #endif
                #if UNITY_SPECCUBE_BLENDING || UNITY_SPECCUBE_BOX_PROJECTION
                    d.boxMin[0] = unity_SpecCube0_BoxMin;
                    d.boxMin[1] = unity_SpecCube1_BoxMin;
                #endif
                #if UNITY_SPECCUBE_BOX_PROJECTION
                    d.boxMax[0] = unity_SpecCube0_BoxMax;
                    d.boxMax[1] = unity_SpecCube1_BoxMax;
                    d.probePosition[0] = unity_SpecCube0_ProbePosition;
                    d.probePosition[1] = unity_SpecCube1_ProbePosition;
                #endif
                d.probeHDR[0] = unity_SpecCube0_HDR;
                d.probeHDR[1] = unity_SpecCube1_HDR;
                Unity_GlossyEnvironmentData ugls_en_data;
                ugls_en_data.roughness = 1.0 - gloss;
                ugls_en_data.reflUVW = viewReflectDirection;
                UnityGI gi = UnityGlobalIllumination(d, 1, normalDirection, ugls_en_data );
////// Specular:
                float4 _Specular_var = tex2D(_Specular,TRANSFORM_TEX(i.uv0, _Specular));
                float4 _AmbientOclussion_var = tex2Dlod(_AmbientOclussion,float4(TRANSFORM_TEX(i.uv0, _AmbientOclussion),0.0,_AODetail));
                float node_18 = 0.0;
                float _AOEnable_var = lerp( node_20, max(min(saturate(( _AOLevel > 0.5 ? (1.0-(1.0-2.0*(_AOLevel-0.5))*(1.0-lerp(_AmbientOclussion_var.a,node_20,_AOBurn))) : (2.0*_AOLevel*lerp(_AmbientOclussion_var.a,node_20,_AOBurn)) )),node_20),node_18), _AOEnable );
                float3 specularColor = (_Specular_var.a*_SpecularLevel*_AOEnable_var*_SpecColor.rgb).r;
                float specularMonochrome;
                float4 _Base_var = tex2D(_Base,TRANSFORM_TEX(i.uv0, _Base));
                float4 _MaskMap_var = tex2D(_MaskMap,TRANSFORM_TEX(i.uv0, _MaskMap));
                float4 _ReflectionMask_var = tex2D(_ReflectionMask,TRANSFORM_TEX(i.uv0, _ReflectionMask));
                float3 node_65 = (_ReflectionTint.rgb*(texCUBE(_ReflectionMap,viewReflectDirection).rgb*_ReflectionMask_var.a)*_ReflectionPower);
                float3 diffuseColor = saturate(max((_AOEnable_var*lerp( _Base_var.rgb, ((_ColorTint.rgb*_MaskMap_var.a)+(_Base_var.rgb*(1.0 - _MaskMap_var.a))), _ColorMask )),node_65)); // Need this for specular when using metallic
                diffuseColor = DiffuseAndSpecularFromMetallic( diffuseColor, specularColor, specularColor, specularMonochrome );
                specularMonochrome = 1.0-specularMonochrome;
                float NdotV = max(0.0,dot( normalDirection, viewDirection ));
                half grazingTerm = saturate( gloss + specularMonochrome );
                float3 indirectSpecular = (gi.indirect.specular);
                indirectSpecular *= FresnelLerp (specularColor, grazingTerm, NdotV);
/////// Diffuse:
                float3 indirectDiffuse = float3(0,0,0);
                float4 _RimMask_var = tex2D(_RimMask,TRANSFORM_TEX(i.uv0, _RimMask));
                indirectDiffuse += (saturate(min(max(node_18,(pow(1.0-max(0,dot(normalDirection, viewDirection)),_RimFresnel)*_RimPower*_RimColor.rgb)),_RimMask_var.a))+(UNITY_LIGHTMODEL_AMBIENT.rgb*_AmbientalPower)); // Diffuse Ambient Light
                indirectDiffuse += gi.indirect.diffuse;
////// Emissive:
                float3 emissive = (node_65*_ReflectionEmission);
/// Final Color:
                outDiffuse = half4( diffuseColor, 1 );
                outSpecSmoothness = half4( specularColor, gloss );
                outNormal = half4( normalDirection * 0.5 + 0.5, 1 );
                outEmission = half4( (node_65*_ReflectionEmission), 1 );
                outEmission.rgb += indirectSpecular * 1;
                outEmission.rgb += indirectDiffuse * diffuseColor;
                #ifndef UNITY_HDR_ON
                    outEmission.rgb = exp2(-outEmission.rgb);
                #endif
            }
            ENDCG
        }
        Pass {
            Name "FORWARD"
            Tags {
                "LightMode"="ForwardBase"
            }
            
            
            CGPROGRAM
            #pragma hull hull
            #pragma domain domain
            #pragma vertex tessvert
            #pragma fragment frag
            #define UNITY_PASS_FORWARDBASE
            #define SHOULD_SAMPLE_SH ( defined (LIGHTMAP_OFF) && defined(DYNAMICLIGHTMAP_OFF) )
            #define _GLOSSYENV 1
            #include "UnityCG.cginc"
            #include "AutoLight.cginc"
            #include "Lighting.cginc"
            #include "Tessellation.cginc"
            #include "UnityPBSLighting.cginc"
            #include "UnityStandardBRDF.cginc"
            #pragma multi_compile_fwdbase_fullshadows
            #pragma multi_compile LIGHTMAP_OFF LIGHTMAP_ON
            #pragma multi_compile DIRLIGHTMAP_OFF DIRLIGHTMAP_COMBINED DIRLIGHTMAP_SEPARATE
            #pragma multi_compile DYNAMICLIGHTMAP_OFF DYNAMICLIGHTMAP_ON
            #pragma multi_compile_fog
            #pragma only_renderers d3d9 d3d11 glcore gles gles3 metal xboxone ps4 psp2 n3ds wiiu 
            #pragma target 5.0
            uniform sampler2D _Base; uniform float4 _Base_ST;
            uniform sampler2D _AmbientOclussion; uniform float4 _AmbientOclussion_ST;
            uniform sampler2D _Normal; uniform float4 _Normal_ST;
            uniform sampler2D _Specular; uniform float4 _Specular_ST;
            uniform sampler2D _Gloss; uniform float4 _Gloss_ST;
            uniform sampler2D _ReflectionMask; uniform float4 _ReflectionMask_ST;
            uniform sampler2D _TransMask; uniform float4 _TransMask_ST;
            uniform sampler2D _Displace; uniform float4 _Displace_ST;
            uniform samplerCUBE _ReflectionMap;
            uniform float _AOBurn;
            uniform float _AOLevel;
            uniform fixed _AOEnable;
            uniform sampler2D _MaskMap; uniform float4 _MaskMap_ST;
            uniform float4 _ColorTint;
            uniform float _SpecularLevel;
            uniform fixed _CustomGloss;
            uniform float _Shinniness;
            uniform float _NormalSmooth;
            uniform float4 _ReflectionTint;
            uniform float _ReflectionPower;
            uniform float _ReflectionEmission;
            uniform float _TransPower;
            uniform fixed _Translucency;
            uniform float _DispBurn;
            uniform float _DispPower;
            uniform float _Tesellation;
            uniform float _RimFresnel;
            uniform float _RimPower;
            uniform float4 _RimColor;
            uniform sampler2D _RimMask; uniform float4 _RimMask_ST;
            uniform float _AmbientalPower;
            uniform fixed _ColorMask;
            uniform float _AODetail;
            uniform float4 _TransColor;
            struct VertexInput {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                float4 tangent : TANGENT;
                float2 texcoord0 : TEXCOORD0;
                float2 texcoord1 : TEXCOORD1;
                float2 texcoord2 : TEXCOORD2;
            };
            struct VertexOutput {
                float4 pos : SV_POSITION;
                float2 uv0 : TEXCOORD0;
                float2 uv1 : TEXCOORD1;
                float2 uv2 : TEXCOORD2;
                float4 posWorld : TEXCOORD3;
                float3 normalDir : TEXCOORD4;
                float3 tangentDir : TEXCOORD5;
                float3 bitangentDir : TEXCOORD6;
                LIGHTING_COORDS(7,8)
                UNITY_FOG_COORDS(9)
                #if defined(LIGHTMAP_ON) || defined(UNITY_SHOULD_SAMPLE_SH)
                    float4 ambientOrLightmapUV : TEXCOORD10;
                #endif
            };
            VertexOutput vert (VertexInput v) {
                VertexOutput o = (VertexOutput)0;
                o.uv0 = v.texcoord0;
                o.uv1 = v.texcoord1;
                o.uv2 = v.texcoord2;
                #ifdef LIGHTMAP_ON
                    o.ambientOrLightmapUV.xy = v.texcoord1.xy * unity_LightmapST.xy + unity_LightmapST.zw;
                    o.ambientOrLightmapUV.zw = 0;
                #endif
                #ifdef DYNAMICLIGHTMAP_ON
                    o.ambientOrLightmapUV.zw = v.texcoord2.xy * unity_DynamicLightmapST.xy + unity_DynamicLightmapST.zw;
                #endif
                o.normalDir = UnityObjectToWorldNormal(v.normal);
                o.tangentDir = normalize( mul( unity_ObjectToWorld, float4( v.tangent.xyz, 0.0 ) ).xyz );
                o.bitangentDir = normalize(cross(o.normalDir, o.tangentDir) * v.tangent.w);
                o.posWorld = mul(unity_ObjectToWorld, v.vertex);
                float3 lightColor = _LightColor0.rgb;
                o.pos = UnityObjectToClipPos( v.vertex );
                UNITY_TRANSFER_FOG(o,o.pos);
                TRANSFER_VERTEX_TO_FRAGMENT(o)
                return o;
            }
            #ifdef UNITY_CAN_COMPILE_TESSELLATION
                struct TessVertex {
                    float4 vertex : INTERNALTESSPOS;
                    float3 normal : NORMAL;
                    float4 tangent : TANGENT;
                    float2 texcoord0 : TEXCOORD0;
                    float2 texcoord1 : TEXCOORD1;
                    float2 texcoord2 : TEXCOORD2;
                };
                struct OutputPatchConstant {
                    float edge[3]         : SV_TessFactor;
                    float inside          : SV_InsideTessFactor;
                    float3 vTangent[4]    : TANGENT;
                    float2 vUV[4]         : TEXCOORD;
                    float3 vTanUCorner[4] : TANUCORNER;
                    float3 vTanVCorner[4] : TANVCORNER;
                    float4 vCWts          : TANWEIGHTS;
                };
                TessVertex tessvert (VertexInput v) {
                    TessVertex o;
                    o.vertex = v.vertex;
                    o.normal = v.normal;
                    o.tangent = v.tangent;
                    o.texcoord0 = v.texcoord0;
                    o.texcoord1 = v.texcoord1;
                    o.texcoord2 = v.texcoord2;
                    return o;
                }
                void displacement (inout VertexInput v){
                    float node_20 = 1.0;
                    float node_18 = 0.0;
                    float4 _Displace_var = tex2Dlod(_Displace,float4(TRANSFORM_TEX(v.texcoord0, _Displace),0.0,0));
                    v.vertex.xyz += (_DispPower*min(node_20,max(node_18,lerp(node_20,_Displace_var.a,_DispBurn)))*v.normal);
                }
                float Tessellation(TessVertex v){
                    return _Tesellation;
                }
                float4 Tessellation(TessVertex v, TessVertex v1, TessVertex v2){
                    float tv = Tessellation(v);
                    float tv1 = Tessellation(v1);
                    float tv2 = Tessellation(v2);
                    return float4( tv1+tv2, tv2+tv, tv+tv1, tv+tv1+tv2 ) / float4(2,2,2,3);
                }
                OutputPatchConstant hullconst (InputPatch<TessVertex,3> v) {
                    OutputPatchConstant o = (OutputPatchConstant)0;
                    float4 ts = Tessellation( v[0], v[1], v[2] );
                    o.edge[0] = ts.x;
                    o.edge[1] = ts.y;
                    o.edge[2] = ts.z;
                    o.inside = ts.w;
                    return o;
                }
                [domain("tri")]
                [partitioning("fractional_odd")]
                [outputtopology("triangle_cw")]
                [patchconstantfunc("hullconst")]
                [outputcontrolpoints(3)]
                TessVertex hull (InputPatch<TessVertex,3> v, uint id : SV_OutputControlPointID) {
                    return v[id];
                }
                [domain("tri")]
                VertexOutput domain (OutputPatchConstant tessFactors, const OutputPatch<TessVertex,3> vi, float3 bary : SV_DomainLocation) {
                    VertexInput v = (VertexInput)0;
                    v.vertex = vi[0].vertex*bary.x + vi[1].vertex*bary.y + vi[2].vertex*bary.z;
                    v.normal = vi[0].normal*bary.x + vi[1].normal*bary.y + vi[2].normal*bary.z;
                    v.tangent = vi[0].tangent*bary.x + vi[1].tangent*bary.y + vi[2].tangent*bary.z;
                    v.texcoord0 = vi[0].texcoord0*bary.x + vi[1].texcoord0*bary.y + vi[2].texcoord0*bary.z;
                    v.texcoord1 = vi[0].texcoord1*bary.x + vi[1].texcoord1*bary.y + vi[2].texcoord1*bary.z;
                    displacement(v);
                    VertexOutput o = vert(v);
                    return o;
                }
            #endif
            float4 frag(VertexOutput i) : COLOR {
                i.normalDir = normalize(i.normalDir);
                float3x3 tangentTransform = float3x3( i.tangentDir, i.bitangentDir, i.normalDir);
                float3 viewDirection = normalize(_WorldSpaceCameraPos.xyz - i.posWorld.xyz);
                float3 _Normal_var = UnpackNormal(tex2D(_Normal,TRANSFORM_TEX(i.uv0, _Normal)));
                float3 normalLocal = lerp(_Normal_var.rgb,float3(0,0,1),_NormalSmooth);
                float3 normalDirection = normalize(mul( normalLocal, tangentTransform )); // Perturbed normals
                float3 viewReflectDirection = reflect( -viewDirection, normalDirection );
                float3 lightDirection = normalize(_WorldSpaceLightPos0.xyz);
                float3 lightColor = _LightColor0.rgb;
                float3 halfDirection = normalize(viewDirection+lightDirection);
////// Lighting:
                float attenuation = LIGHT_ATTENUATION(i);
                float3 attenColor = attenuation * _LightColor0.xyz;
                float Pi = 3.141592654;
                float InvPi = 0.31830988618;
///////// Gloss:
                float node_20 = 1.0;
                float4 _Gloss_var = tex2D(_Gloss,TRANSFORM_TEX(i.uv0, _Gloss));
                float gloss = (lerp( node_20, _Gloss_var.a, _CustomGloss )*_Shinniness);
                float perceptualRoughness = 1.0 - (lerp( node_20, _Gloss_var.a, _CustomGloss )*_Shinniness);
                float roughness = perceptualRoughness * perceptualRoughness;
                float specPow = exp2( gloss * 10.0 + 1.0 );
/////// GI Data:
                UnityLight light;
                #ifdef LIGHTMAP_OFF
                    light.color = lightColor;
                    light.dir = lightDirection;
                    light.ndotl = LambertTerm (normalDirection, light.dir);
                #else
                    light.color = half3(0.f, 0.f, 0.f);
                    light.ndotl = 0.0f;
                    light.dir = half3(0.f, 0.f, 0.f);
                #endif
                UnityGIInput d;
                d.light = light;
                d.worldPos = i.posWorld.xyz;
                d.worldViewDir = viewDirection;
                d.atten = attenuation;
                #if defined(LIGHTMAP_ON) || defined(DYNAMICLIGHTMAP_ON)
                    d.ambient = 0;
                    d.lightmapUV = i.ambientOrLightmapUV;
                #else
                    d.ambient = i.ambientOrLightmapUV;
                #endif
                #if UNITY_SPECCUBE_BLENDING || UNITY_SPECCUBE_BOX_PROJECTION
                    d.boxMin[0] = unity_SpecCube0_BoxMin;
                    d.boxMin[1] = unity_SpecCube1_BoxMin;
                #endif
                #if UNITY_SPECCUBE_BOX_PROJECTION
                    d.boxMax[0] = unity_SpecCube0_BoxMax;
                    d.boxMax[1] = unity_SpecCube1_BoxMax;
                    d.probePosition[0] = unity_SpecCube0_ProbePosition;
                    d.probePosition[1] = unity_SpecCube1_ProbePosition;
                #endif
                d.probeHDR[0] = unity_SpecCube0_HDR;
                d.probeHDR[1] = unity_SpecCube1_HDR;
                Unity_GlossyEnvironmentData ugls_en_data;
                ugls_en_data.roughness = 1.0 - gloss;
                ugls_en_data.reflUVW = viewReflectDirection;
                UnityGI gi = UnityGlobalIllumination(d, 1, normalDirection, ugls_en_data );
                lightDirection = gi.light.dir;
                lightColor = gi.light.color;
////// Specular:
                float NdotL = saturate(dot( normalDirection, lightDirection ));
                float LdotH = saturate(dot(lightDirection, halfDirection));
                float4 _Specular_var = tex2D(_Specular,TRANSFORM_TEX(i.uv0, _Specular));
                float4 _AmbientOclussion_var = tex2Dlod(_AmbientOclussion,float4(TRANSFORM_TEX(i.uv0, _AmbientOclussion),0.0,_AODetail));
                float node_18 = 0.0;
                float _AOEnable_var = lerp( node_20, max(min(saturate(( _AOLevel > 0.5 ? (1.0-(1.0-2.0*(_AOLevel-0.5))*(1.0-lerp(_AmbientOclussion_var.a,node_20,_AOBurn))) : (2.0*_AOLevel*lerp(_AmbientOclussion_var.a,node_20,_AOBurn)) )),node_20),node_18), _AOEnable );
                float3 specularColor = (_Specular_var.a*_SpecularLevel*_AOEnable_var*_SpecColor.rgb).r;
                float specularMonochrome;
                float4 _Base_var = tex2D(_Base,TRANSFORM_TEX(i.uv0, _Base));
                float4 _MaskMap_var = tex2D(_MaskMap,TRANSFORM_TEX(i.uv0, _MaskMap));
                float4 _ReflectionMask_var = tex2D(_ReflectionMask,TRANSFORM_TEX(i.uv0, _ReflectionMask));
                float3 node_65 = (_ReflectionTint.rgb*(texCUBE(_ReflectionMap,viewReflectDirection).rgb*_ReflectionMask_var.a)*_ReflectionPower);
                float3 diffuseColor = saturate(max((_AOEnable_var*lerp( _Base_var.rgb, ((_ColorTint.rgb*_MaskMap_var.a)+(_Base_var.rgb*(1.0 - _MaskMap_var.a))), _ColorMask )),node_65)); // Need this for specular when using metallic
                diffuseColor = DiffuseAndSpecularFromMetallic( diffuseColor, specularColor, specularColor, specularMonochrome );
                specularMonochrome = 1.0-specularMonochrome;
                float NdotV = abs(dot( normalDirection, viewDirection ));
                float NdotH = saturate(dot( normalDirection, halfDirection ));
                float VdotH = saturate(dot( viewDirection, halfDirection ));
                float visTerm = SmithJointGGXVisibilityTerm( NdotL, NdotV, roughness );
                float normTerm = GGXTerm(NdotH, roughness);
                float specularPBL = (visTerm*normTerm) * UNITY_PI;
                #ifdef UNITY_COLORSPACE_GAMMA
                    specularPBL = sqrt(max(1e-4h, specularPBL));
                #endif
                specularPBL = max(0, specularPBL * NdotL);
                #if defined(_SPECULARHIGHLIGHTS_OFF)
                    specularPBL = 0.0;
                #endif
                half surfaceReduction;
                #ifdef UNITY_COLORSPACE_GAMMA
                    surfaceReduction = 1.0-0.28*roughness*perceptualRoughness;
                #else
                    surfaceReduction = 1.0/(roughness*roughness + 1.0);
                #endif
                specularPBL *= any(specularColor) ? 1.0 : 0.0;
                float3 directSpecular = attenColor*specularPBL*FresnelTerm(specularColor, LdotH);
                half grazingTerm = saturate( gloss + specularMonochrome );
                float3 indirectSpecular = (gi.indirect.specular);
                indirectSpecular *= FresnelLerp (specularColor, grazingTerm, NdotV);
                indirectSpecular *= surfaceReduction;
                float3 specular = (directSpecular + indirectSpecular);
/////// Diffuse:
                NdotL = max(0.0,dot( normalDirection, lightDirection ));
                half fd90 = 0.5 + 2 * LdotH * LdotH * (1-gloss);
                float nlPow5 = Pow5(1-NdotL);
                float nvPow5 = Pow5(1-NdotV);
                float3 directDiffuse = ((1 +(fd90 - 1)*nlPow5) * (1 + (fd90 - 1)*nvPow5) * NdotL) * attenColor;
                float3 indirectDiffuse = float3(0,0,0);
                float4 _RimMask_var = tex2D(_RimMask,TRANSFORM_TEX(i.uv0, _RimMask));
                indirectDiffuse += (saturate(min(max(node_18,(pow(1.0-max(0,dot(normalDirection, viewDirection)),_RimFresnel)*_RimPower*_RimColor.rgb)),_RimMask_var.a))+(UNITY_LIGHTMODEL_AMBIENT.rgb*_AmbientalPower)); // Diffuse Ambient Light
                indirectDiffuse += gi.indirect.diffuse;
                float3 diffuse = (directDiffuse + indirectDiffuse) * diffuseColor;
////// Emissive:
                float3 emissive = (node_65*_ReflectionEmission);
/// Final Color:
                float3 finalColor = diffuse + specular + emissive;
                fixed4 finalRGBA = fixed4(finalColor,1);
                UNITY_APPLY_FOG(i.fogCoord, finalRGBA);
                return finalRGBA;
            }
            ENDCG
        }
        Pass {
            Name "FORWARD_DELTA"
            Tags {
                "LightMode"="ForwardAdd"
            }
            Blend One One
            
            
            CGPROGRAM
            #pragma hull hull
            #pragma domain domain
            #pragma vertex tessvert
            #pragma fragment frag
            #define UNITY_PASS_FORWARDADD
            #define SHOULD_SAMPLE_SH ( defined (LIGHTMAP_OFF) && defined(DYNAMICLIGHTMAP_OFF) )
            #define _GLOSSYENV 1
            #include "UnityCG.cginc"
            #include "AutoLight.cginc"
            #include "Lighting.cginc"
            #include "Tessellation.cginc"
            #include "UnityPBSLighting.cginc"
            #include "UnityStandardBRDF.cginc"
            #pragma multi_compile_fwdadd_fullshadows
            #pragma multi_compile LIGHTMAP_OFF LIGHTMAP_ON
            #pragma multi_compile DIRLIGHTMAP_OFF DIRLIGHTMAP_COMBINED DIRLIGHTMAP_SEPARATE
            #pragma multi_compile DYNAMICLIGHTMAP_OFF DYNAMICLIGHTMAP_ON
            #pragma multi_compile_fog
            #pragma only_renderers d3d9 d3d11 glcore gles gles3 metal xboxone ps4 psp2 n3ds wiiu 
            #pragma target 5.0
            uniform sampler2D _Base; uniform float4 _Base_ST;
            uniform sampler2D _AmbientOclussion; uniform float4 _AmbientOclussion_ST;
            uniform sampler2D _Normal; uniform float4 _Normal_ST;
            uniform sampler2D _Specular; uniform float4 _Specular_ST;
            uniform sampler2D _Gloss; uniform float4 _Gloss_ST;
            uniform sampler2D _ReflectionMask; uniform float4 _ReflectionMask_ST;
            uniform sampler2D _TransMask; uniform float4 _TransMask_ST;
            uniform sampler2D _Displace; uniform float4 _Displace_ST;
            uniform samplerCUBE _ReflectionMap;
            uniform float _AOBurn;
            uniform float _AOLevel;
            uniform fixed _AOEnable;
            uniform sampler2D _MaskMap; uniform float4 _MaskMap_ST;
            uniform float4 _ColorTint;
            uniform float _SpecularLevel;
            uniform fixed _CustomGloss;
            uniform float _Shinniness;
            uniform float _NormalSmooth;
            uniform float4 _ReflectionTint;
            uniform float _ReflectionPower;
            uniform float _ReflectionEmission;
            uniform float _TransPower;
            uniform fixed _Translucency;
            uniform float _DispBurn;
            uniform float _DispPower;
            uniform float _Tesellation;
            uniform fixed _ColorMask;
            uniform float _AODetail;
            uniform float4 _TransColor;
            struct VertexInput {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                float4 tangent : TANGENT;
                float2 texcoord0 : TEXCOORD0;
                float2 texcoord1 : TEXCOORD1;
                float2 texcoord2 : TEXCOORD2;
            };
            struct VertexOutput {
                float4 pos : SV_POSITION;
                float2 uv0 : TEXCOORD0;
                float2 uv1 : TEXCOORD1;
                float2 uv2 : TEXCOORD2;
                float4 posWorld : TEXCOORD3;
                float3 normalDir : TEXCOORD4;
                float3 tangentDir : TEXCOORD5;
                float3 bitangentDir : TEXCOORD6;
                LIGHTING_COORDS(7,8)
                UNITY_FOG_COORDS(9)
            };
            VertexOutput vert (VertexInput v) {
                VertexOutput o = (VertexOutput)0;
                o.uv0 = v.texcoord0;
                o.uv1 = v.texcoord1;
                o.uv2 = v.texcoord2;
                o.normalDir = UnityObjectToWorldNormal(v.normal);
                o.tangentDir = normalize( mul( unity_ObjectToWorld, float4( v.tangent.xyz, 0.0 ) ).xyz );
                o.bitangentDir = normalize(cross(o.normalDir, o.tangentDir) * v.tangent.w);
                o.posWorld = mul(unity_ObjectToWorld, v.vertex);
                float3 lightColor = _LightColor0.rgb;
                o.pos = UnityObjectToClipPos( v.vertex );
                UNITY_TRANSFER_FOG(o,o.pos);
                TRANSFER_VERTEX_TO_FRAGMENT(o)
                return o;
            }
            #ifdef UNITY_CAN_COMPILE_TESSELLATION
                struct TessVertex {
                    float4 vertex : INTERNALTESSPOS;
                    float3 normal : NORMAL;
                    float4 tangent : TANGENT;
                    float2 texcoord0 : TEXCOORD0;
                    float2 texcoord1 : TEXCOORD1;
                    float2 texcoord2 : TEXCOORD2;
                };
                struct OutputPatchConstant {
                    float edge[3]         : SV_TessFactor;
                    float inside          : SV_InsideTessFactor;
                    float3 vTangent[4]    : TANGENT;
                    float2 vUV[4]         : TEXCOORD;
                    float3 vTanUCorner[4] : TANUCORNER;
                    float3 vTanVCorner[4] : TANVCORNER;
                    float4 vCWts          : TANWEIGHTS;
                };
                TessVertex tessvert (VertexInput v) {
                    TessVertex o;
                    o.vertex = v.vertex;
                    o.normal = v.normal;
                    o.tangent = v.tangent;
                    o.texcoord0 = v.texcoord0;
                    o.texcoord1 = v.texcoord1;
                    o.texcoord2 = v.texcoord2;
                    return o;
                }
                void displacement (inout VertexInput v){
                    float node_20 = 1.0;
                    float node_18 = 0.0;
                    float4 _Displace_var = tex2Dlod(_Displace,float4(TRANSFORM_TEX(v.texcoord0, _Displace),0.0,0));
                    v.vertex.xyz += (_DispPower*min(node_20,max(node_18,lerp(node_20,_Displace_var.a,_DispBurn)))*v.normal);
                }
                float Tessellation(TessVertex v){
                    return _Tesellation;
                }
                float4 Tessellation(TessVertex v, TessVertex v1, TessVertex v2){
                    float tv = Tessellation(v);
                    float tv1 = Tessellation(v1);
                    float tv2 = Tessellation(v2);
                    return float4( tv1+tv2, tv2+tv, tv+tv1, tv+tv1+tv2 ) / float4(2,2,2,3);
                }
                OutputPatchConstant hullconst (InputPatch<TessVertex,3> v) {
                    OutputPatchConstant o = (OutputPatchConstant)0;
                    float4 ts = Tessellation( v[0], v[1], v[2] );
                    o.edge[0] = ts.x;
                    o.edge[1] = ts.y;
                    o.edge[2] = ts.z;
                    o.inside = ts.w;
                    return o;
                }
                [domain("tri")]
                [partitioning("fractional_odd")]
                [outputtopology("triangle_cw")]
                [patchconstantfunc("hullconst")]
                [outputcontrolpoints(3)]
                TessVertex hull (InputPatch<TessVertex,3> v, uint id : SV_OutputControlPointID) {
                    return v[id];
                }
                [domain("tri")]
                VertexOutput domain (OutputPatchConstant tessFactors, const OutputPatch<TessVertex,3> vi, float3 bary : SV_DomainLocation) {
                    VertexInput v = (VertexInput)0;
                    v.vertex = vi[0].vertex*bary.x + vi[1].vertex*bary.y + vi[2].vertex*bary.z;
                    v.normal = vi[0].normal*bary.x + vi[1].normal*bary.y + vi[2].normal*bary.z;
                    v.tangent = vi[0].tangent*bary.x + vi[1].tangent*bary.y + vi[2].tangent*bary.z;
                    v.texcoord0 = vi[0].texcoord0*bary.x + vi[1].texcoord0*bary.y + vi[2].texcoord0*bary.z;
                    v.texcoord1 = vi[0].texcoord1*bary.x + vi[1].texcoord1*bary.y + vi[2].texcoord1*bary.z;
                    displacement(v);
                    VertexOutput o = vert(v);
                    return o;
                }
            #endif
            float4 frag(VertexOutput i) : COLOR {
                i.normalDir = normalize(i.normalDir);
                float3x3 tangentTransform = float3x3( i.tangentDir, i.bitangentDir, i.normalDir);
                float3 viewDirection = normalize(_WorldSpaceCameraPos.xyz - i.posWorld.xyz);
                float3 _Normal_var = UnpackNormal(tex2D(_Normal,TRANSFORM_TEX(i.uv0, _Normal)));
                float3 normalLocal = lerp(_Normal_var.rgb,float3(0,0,1),_NormalSmooth);
                float3 normalDirection = normalize(mul( normalLocal, tangentTransform )); // Perturbed normals
                float3 viewReflectDirection = reflect( -viewDirection, normalDirection );
                float3 lightDirection = normalize(lerp(_WorldSpaceLightPos0.xyz, _WorldSpaceLightPos0.xyz - i.posWorld.xyz,_WorldSpaceLightPos0.w));
                float3 lightColor = _LightColor0.rgb;
                float3 halfDirection = normalize(viewDirection+lightDirection);
////// Lighting:
                float attenuation = LIGHT_ATTENUATION(i);
                float3 attenColor = attenuation * _LightColor0.xyz;
                float Pi = 3.141592654;
                float InvPi = 0.31830988618;
///////// Gloss:
                float node_20 = 1.0;
                float4 _Gloss_var = tex2D(_Gloss,TRANSFORM_TEX(i.uv0, _Gloss));
                float gloss = (lerp( node_20, _Gloss_var.a, _CustomGloss )*_Shinniness);
                float perceptualRoughness = 1.0 - (lerp( node_20, _Gloss_var.a, _CustomGloss )*_Shinniness);
                float roughness = perceptualRoughness * perceptualRoughness;
                float specPow = exp2( gloss * 10.0 + 1.0 );
////// Specular:
                float NdotL = saturate(dot( normalDirection, lightDirection ));
                float LdotH = saturate(dot(lightDirection, halfDirection));
                float4 _Specular_var = tex2D(_Specular,TRANSFORM_TEX(i.uv0, _Specular));
                float4 _AmbientOclussion_var = tex2Dlod(_AmbientOclussion,float4(TRANSFORM_TEX(i.uv0, _AmbientOclussion),0.0,_AODetail));
                float node_18 = 0.0;
                float _AOEnable_var = lerp( node_20, max(min(saturate(( _AOLevel > 0.5 ? (1.0-(1.0-2.0*(_AOLevel-0.5))*(1.0-lerp(_AmbientOclussion_var.a,node_20,_AOBurn))) : (2.0*_AOLevel*lerp(_AmbientOclussion_var.a,node_20,_AOBurn)) )),node_20),node_18), _AOEnable );
                float3 specularColor = (_Specular_var.a*_SpecularLevel*_AOEnable_var*_SpecColor.rgb).r;
                float specularMonochrome;
                float4 _Base_var = tex2D(_Base,TRANSFORM_TEX(i.uv0, _Base));
                float4 _MaskMap_var = tex2D(_MaskMap,TRANSFORM_TEX(i.uv0, _MaskMap));
                float4 _ReflectionMask_var = tex2D(_ReflectionMask,TRANSFORM_TEX(i.uv0, _ReflectionMask));
                float3 node_65 = (_ReflectionTint.rgb*(texCUBE(_ReflectionMap,viewReflectDirection).rgb*_ReflectionMask_var.a)*_ReflectionPower);
                float3 diffuseColor = saturate(max((_AOEnable_var*lerp( _Base_var.rgb, ((_ColorTint.rgb*_MaskMap_var.a)+(_Base_var.rgb*(1.0 - _MaskMap_var.a))), _ColorMask )),node_65)); // Need this for specular when using metallic
                diffuseColor = DiffuseAndSpecularFromMetallic( diffuseColor, specularColor, specularColor, specularMonochrome );
                specularMonochrome = 1.0-specularMonochrome;
                float NdotV = abs(dot( normalDirection, viewDirection ));
                float NdotH = saturate(dot( normalDirection, halfDirection ));
                float VdotH = saturate(dot( viewDirection, halfDirection ));
                float visTerm = SmithJointGGXVisibilityTerm( NdotL, NdotV, roughness );
                float normTerm = GGXTerm(NdotH, roughness);
                float specularPBL = (visTerm*normTerm) * UNITY_PI;
                #ifdef UNITY_COLORSPACE_GAMMA
                    specularPBL = sqrt(max(1e-4h, specularPBL));
                #endif
                specularPBL = max(0, specularPBL * NdotL);
                #if defined(_SPECULARHIGHLIGHTS_OFF)
                    specularPBL = 0.0;
                #endif
                specularPBL *= any(specularColor) ? 1.0 : 0.0;
                float3 directSpecular = attenColor*specularPBL*FresnelTerm(specularColor, LdotH);
                float3 specular = directSpecular;
/////// Diffuse:
                NdotL = max(0.0,dot( normalDirection, lightDirection ));
                half fd90 = 0.5 + 2 * LdotH * LdotH * (1-gloss);
                float nlPow5 = Pow5(1-NdotL);
                float nvPow5 = Pow5(1-NdotV);
                float3 directDiffuse = ((1 +(fd90 - 1)*nlPow5) * (1 + (fd90 - 1)*nvPow5) * NdotL) * attenColor;
                float3 diffuse = directDiffuse * diffuseColor;
/// Final Color:
                float3 finalColor = diffuse + specular;
                fixed4 finalRGBA = fixed4(finalColor * 1,0);
                UNITY_APPLY_FOG(i.fogCoord, finalRGBA);
                return finalRGBA;
            }
            ENDCG
        }
        Pass {
            Name "ShadowCaster"
            Tags {
                "LightMode"="ShadowCaster"
            }
            Offset 1, 1
            Cull Back
            
            CGPROGRAM
            #pragma hull hull
            #pragma domain domain
            #pragma vertex tessvert
            #pragma fragment frag
            #define UNITY_PASS_SHADOWCASTER
            #define SHOULD_SAMPLE_SH ( defined (LIGHTMAP_OFF) && defined(DYNAMICLIGHTMAP_OFF) )
            #define _GLOSSYENV 1
            #include "UnityCG.cginc"
            #include "Lighting.cginc"
            #include "Tessellation.cginc"
            #include "UnityPBSLighting.cginc"
            #include "UnityStandardBRDF.cginc"
            #pragma fragmentoption ARB_precision_hint_fastest
            #pragma multi_compile_shadowcaster
            #pragma multi_compile LIGHTMAP_OFF LIGHTMAP_ON
            #pragma multi_compile DIRLIGHTMAP_OFF DIRLIGHTMAP_COMBINED DIRLIGHTMAP_SEPARATE
            #pragma multi_compile DYNAMICLIGHTMAP_OFF DYNAMICLIGHTMAP_ON
            #pragma multi_compile_fog
            #pragma only_renderers d3d9 d3d11 glcore gles gles3 metal xboxone ps4 psp2 n3ds wiiu 
            #pragma target 5.0
            uniform sampler2D _Displace; uniform float4 _Displace_ST;
            uniform float _DispBurn;
            uniform float _DispPower;
            uniform float _Tesellation;
            struct VertexInput {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                float4 tangent : TANGENT;
                float2 texcoord0 : TEXCOORD0;
                float2 texcoord1 : TEXCOORD1;
                float2 texcoord2 : TEXCOORD2;
            };
            struct VertexOutput {
                V2F_SHADOW_CASTER;
                float2 uv0 : TEXCOORD1;
                float2 uv1 : TEXCOORD2;
                float2 uv2 : TEXCOORD3;
                float4 posWorld : TEXCOORD4;
                float3 normalDir : TEXCOORD5;
            };
            VertexOutput vert (VertexInput v) {
                VertexOutput o = (VertexOutput)0;
                o.uv0 = v.texcoord0;
                o.uv1 = v.texcoord1;
                o.uv2 = v.texcoord2;
                o.normalDir = UnityObjectToWorldNormal(v.normal);
                o.posWorld = mul(unity_ObjectToWorld, v.vertex);
                o.pos = UnityObjectToClipPos( v.vertex );
                TRANSFER_SHADOW_CASTER(o)
                return o;
            }
            #ifdef UNITY_CAN_COMPILE_TESSELLATION
                struct TessVertex {
                    float4 vertex : INTERNALTESSPOS;
                    float3 normal : NORMAL;
                    float4 tangent : TANGENT;
                    float2 texcoord0 : TEXCOORD0;
                    float2 texcoord1 : TEXCOORD1;
                    float2 texcoord2 : TEXCOORD2;
                };
                struct OutputPatchConstant {
                    float edge[3]         : SV_TessFactor;
                    float inside          : SV_InsideTessFactor;
                    float3 vTangent[4]    : TANGENT;
                    float2 vUV[4]         : TEXCOORD;
                    float3 vTanUCorner[4] : TANUCORNER;
                    float3 vTanVCorner[4] : TANVCORNER;
                    float4 vCWts          : TANWEIGHTS;
                };
                TessVertex tessvert (VertexInput v) {
                    TessVertex o;
                    o.vertex = v.vertex;
                    o.normal = v.normal;
                    o.tangent = v.tangent;
                    o.texcoord0 = v.texcoord0;
                    o.texcoord1 = v.texcoord1;
                    o.texcoord2 = v.texcoord2;
                    return o;
                }
                void displacement (inout VertexInput v){
                    float node_20 = 1.0;
                    float node_18 = 0.0;
                    float4 _Displace_var = tex2Dlod(_Displace,float4(TRANSFORM_TEX(v.texcoord0, _Displace),0.0,0));
                    v.vertex.xyz += (_DispPower*min(node_20,max(node_18,lerp(node_20,_Displace_var.a,_DispBurn)))*v.normal);
                }
                float Tessellation(TessVertex v){
                    return _Tesellation;
                }
                float4 Tessellation(TessVertex v, TessVertex v1, TessVertex v2){
                    float tv = Tessellation(v);
                    float tv1 = Tessellation(v1);
                    float tv2 = Tessellation(v2);
                    return float4( tv1+tv2, tv2+tv, tv+tv1, tv+tv1+tv2 ) / float4(2,2,2,3);
                }
                OutputPatchConstant hullconst (InputPatch<TessVertex,3> v) {
                    OutputPatchConstant o = (OutputPatchConstant)0;
                    float4 ts = Tessellation( v[0], v[1], v[2] );
                    o.edge[0] = ts.x;
                    o.edge[1] = ts.y;
                    o.edge[2] = ts.z;
                    o.inside = ts.w;
                    return o;
                }
                [domain("tri")]
                [partitioning("fractional_odd")]
                [outputtopology("triangle_cw")]
                [patchconstantfunc("hullconst")]
                [outputcontrolpoints(3)]
                TessVertex hull (InputPatch<TessVertex,3> v, uint id : SV_OutputControlPointID) {
                    return v[id];
                }
                [domain("tri")]
                VertexOutput domain (OutputPatchConstant tessFactors, const OutputPatch<TessVertex,3> vi, float3 bary : SV_DomainLocation) {
                    VertexInput v = (VertexInput)0;
                    v.vertex = vi[0].vertex*bary.x + vi[1].vertex*bary.y + vi[2].vertex*bary.z;
                    v.normal = vi[0].normal*bary.x + vi[1].normal*bary.y + vi[2].normal*bary.z;
                    v.tangent = vi[0].tangent*bary.x + vi[1].tangent*bary.y + vi[2].tangent*bary.z;
                    v.texcoord0 = vi[0].texcoord0*bary.x + vi[1].texcoord0*bary.y + vi[2].texcoord0*bary.z;
                    v.texcoord1 = vi[0].texcoord1*bary.x + vi[1].texcoord1*bary.y + vi[2].texcoord1*bary.z;
                    displacement(v);
                    VertexOutput o = vert(v);
                    return o;
                }
            #endif
            float4 frag(VertexOutput i) : COLOR {
                i.normalDir = normalize(i.normalDir);
                float3 viewDirection = normalize(_WorldSpaceCameraPos.xyz - i.posWorld.xyz);
                float3 normalDirection = i.normalDir;
                SHADOW_CASTER_FRAGMENT(i)
            }
            ENDCG
        }
        Pass {
            Name "Meta"
            Tags {
                "LightMode"="Meta"
            }
            Cull Off
            
            CGPROGRAM
            #pragma hull hull
            #pragma domain domain
            #pragma vertex tessvert
            #pragma fragment frag
            #define UNITY_PASS_META 1
            #define SHOULD_SAMPLE_SH ( defined (LIGHTMAP_OFF) && defined(DYNAMICLIGHTMAP_OFF) )
            #define _GLOSSYENV 1
            #include "UnityCG.cginc"
            #include "Lighting.cginc"
            #include "Tessellation.cginc"
            #include "UnityPBSLighting.cginc"
            #include "UnityStandardBRDF.cginc"
            #include "UnityMetaPass.cginc"
            #pragma fragmentoption ARB_precision_hint_fastest
            #pragma multi_compile_shadowcaster
            #pragma multi_compile LIGHTMAP_OFF LIGHTMAP_ON
            #pragma multi_compile DIRLIGHTMAP_OFF DIRLIGHTMAP_COMBINED DIRLIGHTMAP_SEPARATE
            #pragma multi_compile DYNAMICLIGHTMAP_OFF DYNAMICLIGHTMAP_ON
            #pragma multi_compile_fog
            #pragma only_renderers d3d9 d3d11 glcore gles gles3 metal xboxone ps4 psp2 n3ds wiiu 
            #pragma target 5.0
            uniform sampler2D _Base; uniform float4 _Base_ST;
            uniform sampler2D _AmbientOclussion; uniform float4 _AmbientOclussion_ST;
            uniform sampler2D _Specular; uniform float4 _Specular_ST;
            uniform sampler2D _Gloss; uniform float4 _Gloss_ST;
            uniform sampler2D _ReflectionMask; uniform float4 _ReflectionMask_ST;
            uniform sampler2D _Displace; uniform float4 _Displace_ST;
            uniform samplerCUBE _ReflectionMap;
            uniform float _AOBurn;
            uniform float _AOLevel;
            uniform fixed _AOEnable;
            uniform sampler2D _MaskMap; uniform float4 _MaskMap_ST;
            uniform float4 _ColorTint;
            uniform float _SpecularLevel;
            uniform fixed _CustomGloss;
            uniform float _Shinniness;
            uniform float4 _ReflectionTint;
            uniform float _ReflectionPower;
            uniform float _ReflectionEmission;
            uniform float _DispBurn;
            uniform float _DispPower;
            uniform float _Tesellation;
            uniform fixed _ColorMask;
            uniform float _AODetail;
            struct VertexInput {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                float4 tangent : TANGENT;
                float2 texcoord0 : TEXCOORD0;
                float2 texcoord1 : TEXCOORD1;
                float2 texcoord2 : TEXCOORD2;
            };
            struct VertexOutput {
                float4 pos : SV_POSITION;
                float2 uv0 : TEXCOORD0;
                float2 uv1 : TEXCOORD1;
                float2 uv2 : TEXCOORD2;
                float4 posWorld : TEXCOORD3;
                float3 normalDir : TEXCOORD4;
            };
            VertexOutput vert (VertexInput v) {
                VertexOutput o = (VertexOutput)0;
                o.uv0 = v.texcoord0;
                o.uv1 = v.texcoord1;
                o.uv2 = v.texcoord2;
                o.normalDir = UnityObjectToWorldNormal(v.normal);
                o.posWorld = mul(unity_ObjectToWorld, v.vertex);
                o.pos = UnityMetaVertexPosition(v.vertex, v.texcoord1.xy, v.texcoord2.xy, unity_LightmapST, unity_DynamicLightmapST );
                return o;
            }
            #ifdef UNITY_CAN_COMPILE_TESSELLATION
                struct TessVertex {
                    float4 vertex : INTERNALTESSPOS;
                    float3 normal : NORMAL;
                    float4 tangent : TANGENT;
                    float2 texcoord0 : TEXCOORD0;
                    float2 texcoord1 : TEXCOORD1;
                    float2 texcoord2 : TEXCOORD2;
                };
                struct OutputPatchConstant {
                    float edge[3]         : SV_TessFactor;
                    float inside          : SV_InsideTessFactor;
                    float3 vTangent[4]    : TANGENT;
                    float2 vUV[4]         : TEXCOORD;
                    float3 vTanUCorner[4] : TANUCORNER;
                    float3 vTanVCorner[4] : TANVCORNER;
                    float4 vCWts          : TANWEIGHTS;
                };
                TessVertex tessvert (VertexInput v) {
                    TessVertex o;
                    o.vertex = v.vertex;
                    o.normal = v.normal;
                    o.tangent = v.tangent;
                    o.texcoord0 = v.texcoord0;
                    o.texcoord1 = v.texcoord1;
                    o.texcoord2 = v.texcoord2;
                    return o;
                }
                void displacement (inout VertexInput v){
                    float node_20 = 1.0;
                    float node_18 = 0.0;
                    float4 _Displace_var = tex2Dlod(_Displace,float4(TRANSFORM_TEX(v.texcoord0, _Displace),0.0,0));
                    v.vertex.xyz += (_DispPower*min(node_20,max(node_18,lerp(node_20,_Displace_var.a,_DispBurn)))*v.normal);
                }
                float Tessellation(TessVertex v){
                    return _Tesellation;
                }
                float4 Tessellation(TessVertex v, TessVertex v1, TessVertex v2){
                    float tv = Tessellation(v);
                    float tv1 = Tessellation(v1);
                    float tv2 = Tessellation(v2);
                    return float4( tv1+tv2, tv2+tv, tv+tv1, tv+tv1+tv2 ) / float4(2,2,2,3);
                }
                OutputPatchConstant hullconst (InputPatch<TessVertex,3> v) {
                    OutputPatchConstant o = (OutputPatchConstant)0;
                    float4 ts = Tessellation( v[0], v[1], v[2] );
                    o.edge[0] = ts.x;
                    o.edge[1] = ts.y;
                    o.edge[2] = ts.z;
                    o.inside = ts.w;
                    return o;
                }
                [domain("tri")]
                [partitioning("fractional_odd")]
                [outputtopology("triangle_cw")]
                [patchconstantfunc("hullconst")]
                [outputcontrolpoints(3)]
                TessVertex hull (InputPatch<TessVertex,3> v, uint id : SV_OutputControlPointID) {
                    return v[id];
                }
                [domain("tri")]
                VertexOutput domain (OutputPatchConstant tessFactors, const OutputPatch<TessVertex,3> vi, float3 bary : SV_DomainLocation) {
                    VertexInput v = (VertexInput)0;
                    v.vertex = vi[0].vertex*bary.x + vi[1].vertex*bary.y + vi[2].vertex*bary.z;
                    v.normal = vi[0].normal*bary.x + vi[1].normal*bary.y + vi[2].normal*bary.z;
                    v.tangent = vi[0].tangent*bary.x + vi[1].tangent*bary.y + vi[2].tangent*bary.z;
                    v.texcoord0 = vi[0].texcoord0*bary.x + vi[1].texcoord0*bary.y + vi[2].texcoord0*bary.z;
                    v.texcoord1 = vi[0].texcoord1*bary.x + vi[1].texcoord1*bary.y + vi[2].texcoord1*bary.z;
                    displacement(v);
                    VertexOutput o = vert(v);
                    return o;
                }
            #endif
            float4 frag(VertexOutput i) : SV_Target {
                i.normalDir = normalize(i.normalDir);
                float3 viewDirection = normalize(_WorldSpaceCameraPos.xyz - i.posWorld.xyz);
                float3 normalDirection = i.normalDir;
                float3 viewReflectDirection = reflect( -viewDirection, normalDirection );
                UnityMetaInput o;
                UNITY_INITIALIZE_OUTPUT( UnityMetaInput, o );
                
                float4 _ReflectionMask_var = tex2D(_ReflectionMask,TRANSFORM_TEX(i.uv0, _ReflectionMask));
                float3 node_65 = (_ReflectionTint.rgb*(texCUBE(_ReflectionMap,viewReflectDirection).rgb*_ReflectionMask_var.a)*_ReflectionPower);
                o.Emission = (node_65*_ReflectionEmission);
                
                float node_20 = 1.0;
                float4 _AmbientOclussion_var = tex2Dlod(_AmbientOclussion,float4(TRANSFORM_TEX(i.uv0, _AmbientOclussion),0.0,_AODetail));
                float node_18 = 0.0;
                float _AOEnable_var = lerp( node_20, max(min(saturate(( _AOLevel > 0.5 ? (1.0-(1.0-2.0*(_AOLevel-0.5))*(1.0-lerp(_AmbientOclussion_var.a,node_20,_AOBurn))) : (2.0*_AOLevel*lerp(_AmbientOclussion_var.a,node_20,_AOBurn)) )),node_20),node_18), _AOEnable );
                float4 _Base_var = tex2D(_Base,TRANSFORM_TEX(i.uv0, _Base));
                float4 _MaskMap_var = tex2D(_MaskMap,TRANSFORM_TEX(i.uv0, _MaskMap));
                float3 diffColor = saturate(max((_AOEnable_var*lerp( _Base_var.rgb, ((_ColorTint.rgb*_MaskMap_var.a)+(_Base_var.rgb*(1.0 - _MaskMap_var.a))), _ColorMask )),node_65));
                float specularMonochrome;
                float3 specColor;
                float4 _Specular_var = tex2D(_Specular,TRANSFORM_TEX(i.uv0, _Specular));
                diffColor = DiffuseAndSpecularFromMetallic( diffColor, (_Specular_var.a*_SpecularLevel*_AOEnable_var*_SpecColor.rgb).r, specColor, specularMonochrome );
                float4 _Gloss_var = tex2D(_Gloss,TRANSFORM_TEX(i.uv0, _Gloss));
                float roughness = 1.0 - (lerp( node_20, _Gloss_var.a, _CustomGloss )*_Shinniness);
                o.Albedo = diffColor + specColor * roughness * roughness * 0.5;
                
                return UnityMetaFragment( o );
            }
            ENDCG
        }
    }
    FallBack "Diffuse"
    CustomEditor "ShaderForgeMaterialInspector"
}
