using System.Collections.Generic;
using UnityEngine;

namespace SoulCraft.Factory
{
    public static class ShaderFactory
    {
        private static readonly Dictionary<string, Material> _materialCache = new Dictionary<string, Material>();

        private static readonly Dictionary<string, string> _shaderMap = new Dictionary<string, string>
        {
            { "sprite_default",   "Sprites/Default" },
            { "sprite_flash",     "Custom/SpriteFlash" },
            { "sprite_outline",   "Custom/SpriteOutline" },
            { "sprite_glow",      "Custom/SpriteGlow" },
            { "sprite_dissolve",  "Custom/SpriteDissolve" },
            { "screen_damage",    "Custom/ScreenDamage" },
            { "particle_additive", "Particles/Additive" },
        };

        private const string FallbackShaderName = "Sprites/Default";

        /// <summary>
        /// Returns a cached Material for the given name.
        /// If the material has not been created yet, it will be created and cached.
        /// </summary>
        public static Material GetMaterial(string name)
        {
            if (_materialCache.TryGetValue(name, out Material cachedMaterial))
            {
                if (cachedMaterial != null)
                {
                    return cachedMaterial;
                }

                // Material was destroyed; remove stale entry and recreate
                _materialCache.Remove(name);
            }

            Material material = CreateMaterial(name);
            _materialCache[name] = material;
            return material;
        }

        private static Material CreateMaterial(string name)
        {
            string shaderName;

            if (!_shaderMap.TryGetValue(name, out shaderName))
            {
                Debug.LogWarning($"[ShaderFactory] Unknown material name: '{name}'. Using fallback shader.");
                shaderName = FallbackShaderName;
            }

            Shader shader = Shader.Find(shaderName);

            if (shader == null)
            {
                Debug.LogWarning($"[ShaderFactory] Shader '{shaderName}' not found. Falling back to '{FallbackShaderName}'.");
                shader = Shader.Find(FallbackShaderName);
            }

            if (shader == null)
            {
                Debug.LogError($"[ShaderFactory] Fallback shader '{FallbackShaderName}' not found. Returning null.");
                return null;
            }

            Material material = new Material(shader)
            {
                name = $"ShaderFactory_{name}"
            };

            // Apply default settings for specific material types
            ApplyDefaults(name, material);

            return material;
        }

        private static void ApplyDefaults(string name, Material material)
        {
            switch (name)
            {
                case "sprite_flash":
                    material.SetColor("_FlashColor", Color.white);
                    material.SetFloat("_FlashAmount", 0f);
                    break;

                case "sprite_outline":
                    material.SetColor("_OutlineColor", Color.red);
                    material.SetFloat("_OutlineSize", 1f);
                    break;

                case "sprite_glow":
                    material.SetColor("_GlowColor", new Color(1f, 1f, 0f, 1f));
                    material.SetFloat("_GlowSize", 2f);
                    material.SetFloat("_GlowIntensity", 1f);
                    break;

                case "sprite_dissolve":
                    material.SetFloat("_DissolveAmount", 0f);
                    material.SetColor("_EdgeColor", Color.red);
                    material.SetFloat("_EdgeWidth", 0.05f);
                    break;

                case "screen_damage":
                    material.SetFloat("_DamageAmount", 0f);
                    material.SetColor("_VignetteColor", new Color(1f, 0f, 0f, 0.8f));
                    break;
            }
        }

        /// <summary>
        /// Clears all cached materials.
        /// Call this when changing scenes or during cleanup to prevent memory leaks.
        /// </summary>
        public static void ClearCache()
        {
            foreach (var kvp in _materialCache)
            {
                if (kvp.Value != null)
                {
                    Object.Destroy(kvp.Value);
                }
            }

            _materialCache.Clear();
        }

        /// <summary>
        /// Checks whether a material with the given name has been cached.
        /// </summary>
        public static bool HasCachedMaterial(string name)
        {
            return _materialCache.ContainsKey(name) && _materialCache[name] != null;
        }
    }
}
