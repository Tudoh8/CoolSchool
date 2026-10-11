using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace CoolSchool.EditorTools
{
    /// <summary>
    /// Creates the particle prefabs for the knights slice (confetti, block spark, sparkle, swoosh and the
    /// dragon's looping fire confetti) in Assets/CoolSchool/Prefabs/Effects, with URP Particles/Unlit materials.
    /// Everything is configured in code, so re-running replaces the assets.
    /// </summary>
    public static class BattleEffectsBuilder
    {
        const string RootFolder = "Assets/CoolSchool";
        const string EffectsFolder = RootFolder + "/Prefabs/Effects";
        const string QuadMaterialPath = RootFolder + "/Materials/ParticleQuad.mat";
        const string SoftMaterialPath = RootFolder + "/Materials/ParticleSoft.mat";

        /// <summary>The prefabs' ParticleSystems (any of them can be null if creation failed).</summary>
        public sealed class Result
        {
            public ParticleSystem Confetti;
            public ParticleSystem BlockSpark;
            public ParticleSystem Sparkle;
            public ParticleSystem Swoosh;
            public ParticleSystem FireConfetti;
        }

        public static Result CreateAll()
        {
            PlaygroundPrototypeBuilder.EnsureFolder(RootFolder, "Materials");
            PlaygroundPrototypeBuilder.EnsureFolder(RootFolder, "Prefabs");
            PlaygroundPrototypeBuilder.EnsureFolder(RootFolder + "/Prefabs", "Effects");

            Material quad = CreateParticleMaterial(QuadMaterialPath, null);
            Material soft = CreateParticleMaterial(SoftMaterialPath,
                AssetDatabase.GetBuiltinExtraResource<Texture2D>("Default-Particle.psd"));
            if (quad == null || soft == null) return new Result();

            return new Result
            {
                Confetti = SavePrefab(BuildConfetti(quad), "Confetti"),
                BlockSpark = SavePrefab(BuildBlockSpark(soft), "BlockSpark"),
                Sparkle = SavePrefab(BuildSparkle(soft), "Sparkle"),
                Swoosh = SavePrefab(BuildSwoosh(soft), "Swoosh"),
                FireConfetti = SavePrefab(BuildFireConfetti(quad), "FireConfetti"),
            };
        }

        // ----- Materials -----------------------------------------------------------------------

        static Material CreateParticleMaterial(string path, Texture2D texture)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (shader == null)
            {
                Debug.LogError("CoolSchool: URP Particles/Unlit shader not found; is URP installed?");
                return null;
            }

            var material = new Material(shader) { name = System.IO.Path.GetFileNameWithoutExtension(path) };
            material.SetColor("_BaseColor", Color.white);
            if (texture != null) material.SetTexture("_BaseMap", texture);
            material.SetFloat("_Cull", 0f);
            SetAlphaBlended(material);

            AssetDatabase.DeleteAsset(path);
            AssetDatabase.CreateAsset(material, path);
            return AssetDatabase.LoadAssetAtPath<Material>(path);
        }

        /// <summary>Puts a URP material into the Transparent / Alpha blend state (no depth write, no shadows).</summary>
        internal static void SetAlphaBlended(Material material)
        {
            material.SetFloat("_Surface", 1f);
            material.SetFloat("_Blend", 0f);
            material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            material.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
            material.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
            material.SetFloat("_ZWrite", 0f);
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.DisableKeyword("_ALPHATEST_ON");
            material.SetOverrideTag("RenderType", "Transparent");
            material.renderQueue = (int)RenderQueue.Transparent;
            material.SetShaderPassEnabled("ShadowCaster", false);
        }

        // ----- Prefab helpers ------------------------------------------------------------------

        /// <summary>A stopped ParticleSystem on a new GameObject, so duration and looping can still be set.</summary>
        static ParticleSystem NewSystem(string name, Material material, out GameObject root)
        {
            root = new GameObject(name, typeof(ParticleSystem));
            var system = root.GetComponent<ParticleSystem>();
            system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var renderer = root.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return system;
        }

        static ParticleSystem SavePrefab(GameObject root, string name)
        {
            if (root == null) return null;

            string path = EffectsFolder + "/" + name + ".prefab";
            AssetDatabase.DeleteAsset(path);
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            return prefab != null ? prefab.GetComponent<ParticleSystem>() : null;
        }

        static Gradient ConfettiColors()
        {
            Color[] colors =
            {
                new Color(1f, 0.25f, 0.3f),
                new Color(1f, 0.6f, 0.15f),
                new Color(1f, 0.9f, 0.2f),
                new Color(0.3f, 0.85f, 0.4f),
                new Color(0.25f, 0.7f, 1f),
                new Color(0.65f, 0.4f, 1f),
                new Color(1f, 0.5f, 0.8f),
            };
            return FixedGradient(colors);
        }

        /// <summary>A hard-stepped gradient, so "random colour" picks one of the listed colours.</summary>
        static Gradient FixedGradient(Color[] colors)
        {
            var keys = new GradientColorKey[colors.Length];
            for (int i = 0; i < colors.Length; i++)
                keys[i] = new GradientColorKey(colors[i], (i + 1f) / colors.Length);

            var gradient = new Gradient
            {
                mode = GradientMode.Fixed,
                colorKeys = keys,
                alphaKeys = new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) },
            };
            return gradient;
        }

        static ParticleSystem.MinMaxGradient RandomOf(Gradient gradient)
        {
            return new ParticleSystem.MinMaxGradient(gradient) { mode = ParticleSystemGradientMode.RandomColor };
        }

        /// <summary>White that stays solid for most of the life and then fades out.</summary>
        static void FadeOut(ParticleSystem system, float solidUntil)
        {
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, solidUntil), new GradientAlphaKey(0f, 1f) });

            ParticleSystem.ColorOverLifetimeModule color = system.colorOverLifetime;
            color.enabled = true;
            color.color = new ParticleSystem.MinMaxGradient(gradient);
        }

        /// <summary>Size multiplier over life from startScale to endScale (linear).</summary>
        static void SizeOverLife(ParticleSystem system, float startScale, float endScale)
        {
            ParticleSystem.SizeOverLifetimeModule size = system.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, startScale, 1f, endScale));
        }

        static void Spin(ParticleSystem system, float degreesPerSecond)
        {
            ParticleSystem.RotationOverLifetimeModule rotation = system.rotationOverLifetime;
            rotation.enabled = true;
            rotation.z = new ParticleSystem.MinMaxCurve(-degreesPerSecond * Mathf.Deg2Rad, degreesPerSecond * Mathf.Deg2Rad);
        }

        // ----- The effects ---------------------------------------------------------------------

        /// <summary>A pop of about 40 small multi-coloured paper rectangles that tumble down.</summary>
        static GameObject BuildConfetti(Material material)
        {
            ParticleSystem system = NewSystem("Confetti", material, out GameObject root);

            ParticleSystem.MainModule main = system.main;
            main.duration = 0.5f;
            main.loop = false;
            main.playOnAwake = true;
            main.stopAction = ParticleSystemStopAction.Destroy;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 80;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.9f, 1.5f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(2.5f, 5f);
            main.gravityModifier = 0.9f;
            main.startSize3D = true;
            main.startSizeX = new ParticleSystem.MinMaxCurve(0.07f, 0.12f);
            main.startSizeY = new ParticleSystem.MinMaxCurve(0.04f, 0.07f);
            main.startSizeZ = new ParticleSystem.MinMaxCurve(1f);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startColor = RandomOf(ConfettiColors());

            ParticleSystem.EmissionModule emission = system.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 40) });

            ParticleSystem.ShapeModule shape = system.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 45f;
            shape.radius = 0.1f;
            shape.rotation = new Vector3(-90f, 0f, 0f);

            Spin(system, 360f);
            FadeOut(system, 0.7f);
            return root;
        }

        /// <summary>A quick white star burst where a blocked hit lands.</summary>
        static GameObject BuildBlockSpark(Material material)
        {
            ParticleSystem system = NewSystem("BlockSpark", material, out GameObject root);

            ParticleSystem.MainModule main = system.main;
            main.duration = 0.3f;
            main.loop = false;
            main.playOnAwake = true;
            main.stopAction = ParticleSystemStopAction.Destroy;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 40;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.2f, 0.4f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(3f, 6f);
            main.gravityModifier = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.1f, 0.2f);
            main.startColor = new ParticleSystem.MinMaxGradient(Color.white, new Color(1f, 0.95f, 0.6f));

            ParticleSystem.EmissionModule emission = system.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 18) });

            ParticleSystem.ShapeModule shape = system.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.05f;

            SizeOverLife(system, 1f, 0f);
            FadeOut(system, 0.5f);
            return root;
        }

        /// <summary>Soft gold twinkles that drift up around the player when imagination kicks in.</summary>
        static GameObject BuildSparkle(Material material)
        {
            ParticleSystem system = NewSystem("Sparkle", material, out GameObject root);

            ParticleSystem.MainModule main = system.main;
            main.duration = 1.2f;
            main.loop = false;
            main.playOnAwake = true;
            main.stopAction = ParticleSystemStopAction.Destroy;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 60;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.8f, 1.4f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.1f, 0.4f);
            main.gravityModifier = -0.08f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.1f, 0.25f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.82f, 0.25f), new Color(1f, 0.97f, 0.7f));

            ParticleSystem.EmissionModule emission = system.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 12), new ParticleSystem.Burst(0.3f, 10), new ParticleSystem.Burst(0.6f, 8) });

            ParticleSystem.ShapeModule shape = system.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 1f;

            // Twinkle: grow quickly, then shrink away.
            ParticleSystem.SizeOverLifetimeModule size = system.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f,
                new AnimationCurve(new Keyframe(0f, 0f), new Keyframe(0.3f, 1f), new Keyframe(1f, 0f)));
            return root;
        }

        /// <summary>A short white puff along the sword swing.</summary>
        static GameObject BuildSwoosh(Material material)
        {
            ParticleSystem system = NewSystem("Swoosh", material, out GameObject root);

            ParticleSystem.MainModule main = system.main;
            main.duration = 0.25f;
            main.loop = false;
            main.playOnAwake = true;
            main.stopAction = ParticleSystemStopAction.Destroy;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 20;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.2f, 0.35f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.3f, 1f);
            main.gravityModifier = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.3f, 0.5f);
            main.startColor = new Color(1f, 1f, 1f, 0.55f);

            ParticleSystem.EmissionModule emission = system.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 7) });

            ParticleSystem.ShapeModule shape = system.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.2f;

            SizeOverLife(system, 0.6f, 1.5f);
            FadeOut(system, 0.2f);
            return root;
        }

        /// <summary>The dragon's breath: a looping forward cone of orange, yellow and red confetti.</summary>
        static GameObject BuildFireConfetti(Material material)
        {
            ParticleSystem system = NewSystem("FireConfetti", material, out GameObject root);

            Gradient colors = FixedGradient(new[]
            {
                new Color(1f, 0.3f, 0.1f),
                new Color(1f, 0.55f, 0.1f),
                new Color(1f, 0.8f, 0.15f),
                new Color(1f, 0.9f, 0.4f),
            });

            ParticleSystem.MainModule main = system.main;
            main.duration = 1.5f;
            main.loop = true;
            main.playOnAwake = false;
            main.stopAction = ParticleSystemStopAction.None;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 200;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.6f, 1f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(5f, 8f);
            main.gravityModifier = 0.25f;
            main.startSize3D = true;
            main.startSizeX = new ParticleSystem.MinMaxCurve(0.1f, 0.2f);
            main.startSizeY = new ParticleSystem.MinMaxCurve(0.05f, 0.1f);
            main.startSizeZ = new ParticleSystem.MinMaxCurve(1f);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startColor = RandomOf(colors);

            ParticleSystem.EmissionModule emission = system.emission;
            emission.rateOverTime = 60f;

            // A cone built on an empty object emits along local +Z, i.e. straight ahead of the mouth.
            ParticleSystem.ShapeModule shape = system.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 22f;
            shape.radius = 0.15f;

            Spin(system, 400f);
            SizeOverLife(system, 1f, 0.4f);
            FadeOut(system, 0.65f);
            return root;
        }
    }
}
