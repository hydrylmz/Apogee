using UnityEngine;
using UnityEngine.Rendering;

namespace Apogee.UI.Flight3D
{
    /// <summary>
    /// Centralized shader/material resolution for the runtime-built 3D Flight Theatre.
    /// Prefers URP shaders (project runs URP 14) and falls back to the always-available
    /// "Sprites/Default" (unlit, vertex-color + alpha blend) when URP shaders are not
    /// found at runtime. All materials are created fresh — no shared asset mutation.
    /// (3D stage plan, Faz 0)
    /// </summary>
    internal static class Flight3DMaterials
    {
        // ── Shader resolution ─────────────────────────────────────────────────────

        /// <summary>Returns the first non-null shader; "Sprites/Default" as last resort.</summary>
        internal static Shader ResolveShader(params string[] names)
        {
            foreach (string name in names)
            {
                Shader s = Shader.Find(name);
                if (s != null) return s;
            }
            return Shader.Find("Sprites/Default");
        }

        internal static Shader LitShader => ResolveShader(
            "Universal Render Pipeline/Lit",
            "Universal Render Pipeline/SimpleLit",
            "Universal Render Pipeline/Unlit");

        internal static Shader UnlitShader => ResolveShader(
            "Universal Render Pipeline/Unlit",
            "Universal Render Pipeline/SimpleLit");

        internal static Shader ParticleShader => Shader.Find("Sprites/Default")
            ?? ResolveShader("Universal Render Pipeline/Particles/Unlit");

        // ── Material factories ────────────────────────────────────────────────────

        /// <summary>Opaque lit material for solid surfaces (vehicle hull, terrain, pad).</summary>
        internal static Material Lit(Color color)
        {
            var mat = new Material(LitShader);
            mat.color = color; // maps to [MainColor] (_BaseColor) on URP shaders
            return mat;
        }

        /// <summary>Opaque unlit material for glow-ish primitives.</summary>
        internal static Material Unlit(Color color)
        {
            var mat = new Material(UnlitShader);
            mat.color = color;
            return mat;
        }

        /// <summary>
        /// Alpha-blended transparent material (atmosphere shells, vapour cone,
        /// heat glow, parachute canopy). Configures the URP surface keywords when
        /// the shader exposes them; harmless no-ops otherwise.
        /// </summary>
        internal static Material Transparent(Color color)
        {
            var mat = new Material(UnlitShader);
            mat.color = color;

            mat.SetFloat("_Surface", 1f);                       // 1 = Transparent
            mat.SetOverrideTag("RenderType", "Transparent");
            mat.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            mat.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            mat.SetFloat("_ZWrite", 0f);
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            mat.renderQueue = (int)RenderQueue.Transparent;
            return mat;
        }

        /// <summary>Particle material — vertex-color driven (Sprites/Default).</summary>
        internal static Material Particle()
        {
            return new Material(ParticleShader);
        }

        /// <summary>Line material — vertex-color driven, alpha-capable.</summary>
        internal static Material Line()
        {
            return new Material(ParticleShader);
        }
    }
}
