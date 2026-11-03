using System.IO;
using System.Reflection;
using UnityEngine;

namespace SokudoTuners
{
    // Images embedded in the DLL, so the mod stays a single file.
    public static class ModAssets
    {
        private static Sprite _icon;
        private static Sprite _pinGlyph;

        public static Sprite Icon => _icon != null ? _icon : _icon = LoadSprite("icon.png");
        public static Sprite PinGlyph => _pinGlyph != null ? _pinGlyph : _pinGlyph = LoadSprite("pin.png");

        public static Texture2D LoadTexture(string name)
        {
            using (Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("SokudoTuners." + name))
            {
                if (stream == null)
                    return null;
                byte[] bytes = new byte[stream.Length];
                stream.Read(bytes, 0, bytes.Length);

                Texture2D texture = new Texture2D(2, 2, TextureFormat.RGBA32, true);
                texture.LoadImage(bytes);
                texture.wrapMode = TextureWrapMode.Clamp;
                return texture;
            }
        }

        public static Sprite LoadSprite(string name)
        {
            Texture2D texture = LoadTexture(name);
            return texture == null ? null : Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f), 100f);
        }
    }
}
