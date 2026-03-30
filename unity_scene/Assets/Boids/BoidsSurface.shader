Shader "Custom/BoidsURPLit"
{
    Properties
    {
        _Color    ("Color",    Color) = (0.2, 0.6, 1.0, 1)
        _Emission ("Emission", Color) = (0.0, 0.2, 0.8, 1)
        _Scale    ("Scale",    Float) = 0.2
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }

            HLSLPROGRAM
            #pragma vertex   vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #pragma instancing_options procedural:setup
            #pragma target 4.5

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct BoidData
            {
                float3 position;
                float3 velocity;
            };

            #ifdef UNITY_PROCEDURAL_INSTANCING_ENABLED
                StructuredBuffer<BoidData> _BoidsBuffer;
            #endif

            float  _Scale;
            float4 _Color;
            float4 _Emission;

            void setup()
            {
            #ifdef UNITY_PROCEDURAL_INSTANCING_ENABLED
                BoidData b = _BoidsBuffer[unity_InstanceID];

                float3 fwd   = normalize(b.velocity + float3(0.0001, 0, 0));
                float3 worldUp = abs(dot(fwd, float3(0,1,0))) > 0.99f
                               ? float3(1,0,0) : float3(0,1,0);
                float3 right = normalize(cross(worldUp, fwd));
                float3 up    = cross(fwd, right);

                unity_ObjectToWorld  = 0;
                unity_ObjectToWorld._11_21_31 = right * _Scale;
                unity_ObjectToWorld._12_22_32 = up    * _Scale;
                unity_ObjectToWorld._13_23_33 = fwd   * _Scale;
                unity_ObjectToWorld._14_24_34 = b.position;
                unity_ObjectToWorld._44       = 1.0f;

                float s = 1.0f / _Scale;
                unity_WorldToObject  = 0;
                unity_WorldToObject._11_21_31 = float3(right.x, up.x, fwd.x) * s;
                unity_WorldToObject._12_22_32 = float3(right.y, up.y, fwd.y) * s;
                unity_WorldToObject._13_23_33 = float3(right.z, up.z, fwd.z) * s;
                unity_WorldToObject._14       = -dot(right, b.position) * s;
                unity_WorldToObject._24       = -dot(up,    b.position) * s;
                unity_WorldToObject._34       = -dot(fwd,   b.position) * s;
                unity_WorldToObject._44       = 1.0f;
            #endif
            }

            struct Attributes
            {
                float3 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS  : SV_POSITION;
                float3 normalWS    : TEXCOORD0;
                float3 positionWS  : TEXCOORD1;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            Varyings vert(Attributes IN)
            {
                UNITY_SETUP_INSTANCE_ID(IN);
                Varyings OUT;
                UNITY_TRANSFER_INSTANCE_ID(IN, OUT);

                // Procedural TRS applied via unity_ObjectToWorld set in setup()
                float3 posWS  = mul(unity_ObjectToWorld, float4(IN.positionOS, 1)).xyz;
                OUT.positionCS = TransformWorldToHClip(posWS);
                OUT.positionWS = posWS;

                // Normal: multiply by transpose of WorldToObject (= ObjectToWorld for normals)
                OUT.normalWS = normalize(mul((float3x3)unity_ObjectToWorld,
                                             IN.normalOS) / (_Scale * _Scale));
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(IN);

                float3 normalWS = normalize(IN.normalWS);

                // Simple main directional light
                Light mainLight = GetMainLight();
                float NdotL = saturate(dot(normalWS, mainLight.direction));

                float3 color = _Color.rgb * mainLight.color * NdotL
                             + _Color.rgb * 0.15            // ambient fill
                             + _Emission.rgb;

                return half4(color, 1.0);
            }
            ENDHLSL
        }

        // Shadow caster pass so boids cast shadows
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode"="ShadowCaster" }

            ZWrite On
            ColorMask 0

            HLSLPROGRAM
            #pragma vertex   vertShadow
            #pragma fragment fragShadow
            #pragma multi_compile_instancing
            #pragma instancing_options procedural:setup
            #pragma target 4.5

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct BoidData { float3 position; float3 velocity; };

            #ifdef UNITY_PROCEDURAL_INSTANCING_ENABLED
                StructuredBuffer<BoidData> _BoidsBuffer;
            #endif

            float _Scale;

            // Identical setup() — required in every Pass
            void setup()
            {
            #ifdef UNITY_PROCEDURAL_INSTANCING_ENABLED
                BoidData b = _BoidsBuffer[unity_InstanceID];
                float3 fwd   = normalize(b.velocity + float3(0.0001,0,0));
                float3 worldUp = abs(dot(fwd,float3(0,1,0)))>0.99f
                               ? float3(1,0,0):float3(0,1,0);
                float3 right = normalize(cross(worldUp,fwd));
                float3 up    = cross(fwd,right);
                unity_ObjectToWorld=0;
                unity_ObjectToWorld._11_21_31=right*_Scale;
                unity_ObjectToWorld._12_22_32=up*_Scale;
                unity_ObjectToWorld._13_23_33=fwd*_Scale;
                unity_ObjectToWorld._14_24_34=b.position;
                unity_ObjectToWorld._44=1;
                float s=1/_Scale;
                unity_WorldToObject=0;
                unity_WorldToObject._11_21_31=float3(right.x,up.x,fwd.x)*s;
                unity_WorldToObject._12_22_32=float3(right.y,up.y,fwd.y)*s;
                unity_WorldToObject._13_23_33=float3(right.z,up.z,fwd.z)*s;
                unity_WorldToObject._14=-dot(right,b.position)*s;
                unity_WorldToObject._24=-dot(up,b.position)*s;
                unity_WorldToObject._34=-dot(fwd,b.position)*s;
                unity_WorldToObject._44=1;
            #endif
            }

            struct Attributes { float3 positionOS:POSITION; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings   { float4 positionCS:SV_POSITION; UNITY_VERTEX_INPUT_INSTANCE_ID };

            Varyings vertShadow(Attributes IN)
            {
                UNITY_SETUP_INSTANCE_ID(IN);
                Varyings OUT;
                UNITY_TRANSFER_INSTANCE_ID(IN,OUT);
                float3 posWS = mul(unity_ObjectToWorld, float4(IN.positionOS,1)).xyz;
                OUT.positionCS = TransformWorldToHClip(posWS);
                return OUT;
            }

            half4 fragShadow(Varyings IN) : SV_Target { return 0; }
            ENDHLSL
        }
    }
}