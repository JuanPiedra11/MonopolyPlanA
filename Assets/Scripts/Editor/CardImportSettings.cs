using UnityEditor;

namespace MonopolyPlanA.EditorTools
{
    /// <summary>
    /// Las cartas en Resources/Cards se importan SIN reescalar a potencia de 2,
    /// para conservar sus proporciones originales al dibujarlas en la UI.
    /// </summary>
    public class CardImportSettings : AssetPostprocessor
    {
        void OnPreprocessTexture()
        {
            string p = assetPath.Replace('\\', '/');
            bool isBoard = p.Contains("Resources/Board");
            if (!isBoard && !p.Contains("Resources/Cards") && !p.Contains("Resources/Avatars")
                && !p.Contains("Resources/Anims") && !p.Contains("Resources/Dice")
                && !p.Contains("Resources/Poses") && !p.Contains("Resources/Backgrounds")
                && !p.Contains("Resources/UI") && !p.Contains("Resources/VFX")) return;

            bool isBg = p.Contains("Resources/Backgrounds") || p.Contains("Resources/UI");
            var importer = (TextureImporter)assetImporter;
            importer.npotScale = TextureImporterNPOTScale.None; // ¡clave! no deformar
            importer.mipmapEnabled = isBoard;                   // el tablero se ve en ángulo
            importer.maxTextureSize = (isBoard || isBg) ? 2048 : 1024;
            // los fondos y el tablero no llevan alpha; los marcos/rank de UI sí
            importer.alphaIsTransparency = !isBoard && !p.Contains("Resources/Backgrounds");
            // UI legible por CPU para poder limpiar fondos en tiempo de ejecución
            importer.isReadable = p.Contains("Resources/UI");
        }

        /// <summary>
        /// Modelos 3D (Resources/Models): las animaciones de idle y walk deben
        /// reproducirse en bucle (Loop Time), el resto son one-shot.
        /// </summary>
        void OnPreprocessAnimation()
        {
            string p = assetPath.Replace('\\', '/');
            if (!p.Contains("Resources/Models")) return;

            var mi = (ModelImporter)assetImporter;
            var clips = mi.defaultClipAnimations;
            if (clips == null || clips.Length == 0) return;

            bool loop = p.Contains("Idle") || p.Contains("Walk");
            foreach (var c in clips)
                c.loopTime = loop;
            mi.clipAnimations = clips;
        }
    }
}
