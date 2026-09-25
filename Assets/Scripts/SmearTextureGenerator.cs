#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

public class SmearTextureGenerator
{
    [MenuItem("Tools/VFX/Generate Sword Smear Texture")]
    public static void CreateSmearTexture()
    {
        int width = 512;
        int height = 256;
        Texture2D tex = new Texture2D(width, height, TextureFormat.RGBA32, false);

        for (int y = 0; y < height; y++)
        {
            float uvY = (float)y / (height - 1);

            // Tính toán khoảng cách tới lõi giữa vệt chém (0 ở giữa, 1 ở 2 mép)
            float distFromCenterY = Mathf.Abs(uvY - 0.5f) * 2f;

            // Mờ dần mềm mại ra 2 mép trên/dưới (Alpha falloff)
            float alphaY = Mathf.Clamp01(Mathf.Cos(distFromCenterY * Mathf.PI * 0.5f));
            alphaY = Mathf.Pow(alphaY, 1.8f); // Tăng độ mềm của viền

            for (int x = 0; x < width; x++)
            {
                float uvX = (float)x / (width - 1);

                // Mờ dần ở điểm bắt đầu và kết thúc chém (Trục X)
                float alphaX = Mathf.Sin(uvX * Mathf.PI);

                // Tổng hợp Alpha và màu sắc
                float finalAlpha = alphaY * alphaX;
                Color pixelColor = new Color(1f, 1f, 1f, finalAlpha);

                tex.SetPixel(x, y, pixelColor);
            }
        }

        tex.Apply();

        // Lưu file thành PNG
        byte[] bytes = tex.EncodeToPNG();
        string path = "Assets/SwordSmear_Gradient.png";
        File.WriteAllBytes(path, bytes);
        AssetDatabase.Refresh();

        // Tự động cấu hình thông số Texture Importer chuẩn VFX
        TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer != null)
        {
            importer.textureType = TextureImporterType.Default;
            importer.alphaIsTransparency = true; // Đã sửa: đổi thành alphaIsTransparency
            importer.wrapMode = TextureWrapMode.Clamp; // Bắt buộc Clamp để không bị lặp viền
            importer.filterMode = FilterMode.Bilinear;
            importer.SaveAndReimport();
        }

        Debug.Log("<color=green>✓ Đã tạo xong Texture tại: " + path + "</color>");
    }
}
#endif