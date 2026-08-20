using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(CanvasRenderer))]
public class HUDIconGraphic : MaskableGraphic
{
    public enum IconType { Magazine, Health, Rifle, Sniper, Pistol }
    public IconType iconType;

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        Rect r = GetPixelAdjustedRect();
        if (iconType == IconType.Health)
        {
            AddRect(vh, r, 0.38f, 0.08f, 0.62f, 0.92f, color);
            AddRect(vh, r, 0.08f, 0.38f, 0.92f, 0.62f, color);
            return;
        }

        if (iconType == IconType.Rifle)
        {
            AddRect(vh, r, 0.12f, 0.55f, 0.94f, 0.63f, color);
            AddRect(vh, r, 0.32f, 0.42f, 0.70f, 0.66f, color);
            AddQuad(vh, r, new Vector2(0.08f, 0.44f), new Vector2(0.35f, 0.44f),
                new Vector2(0.35f, 0.60f), new Vector2(0.17f, 0.56f), color);
            AddQuad(vh, r, new Vector2(0.53f, 0.18f), new Vector2(0.66f, 0.18f),
                new Vector2(0.62f, 0.44f), new Vector2(0.52f, 0.44f), color);
            AddQuad(vh, r, new Vector2(0.39f, 0.17f), new Vector2(0.51f, 0.20f),
                new Vector2(0.49f, 0.43f), new Vector2(0.38f, 0.43f), color);
            return;
        }

        if (iconType == IconType.Sniper)
        {
            AddRect(vh, r, 0.10f, 0.53f, 0.96f, 0.60f, color);
            AddRect(vh, r, 0.27f, 0.40f, 0.68f, 0.62f, color);
            AddQuad(vh, r, new Vector2(0.05f, 0.41f), new Vector2(0.30f, 0.41f),
                new Vector2(0.30f, 0.57f), new Vector2(0.13f, 0.54f), color);
            AddRect(vh, r, 0.37f, 0.69f, 0.66f, 0.78f, color);
            AddRect(vh, r, 0.42f, 0.60f, 0.46f, 0.70f, color);
            AddRect(vh, r, 0.58f, 0.60f, 0.62f, 0.70f, color);
            AddQuad(vh, r, new Vector2(0.48f, 0.18f), new Vector2(0.61f, 0.18f),
                new Vector2(0.58f, 0.42f), new Vector2(0.47f, 0.42f), color);
            return;
        }

        if (iconType == IconType.Pistol)
        {
            AddRect(vh, r, 0.19f, 0.56f, 0.88f, 0.72f, color);
            AddRect(vh, r, 0.28f, 0.44f, 0.72f, 0.58f, color);
            AddQuad(vh, r, new Vector2(0.43f, 0.13f), new Vector2(0.65f, 0.13f),
                new Vector2(0.61f, 0.46f), new Vector2(0.48f, 0.46f), color);
            AddRect(vh, r, 0.73f, 0.53f, 0.91f, 0.59f, color);
            return;
        }

        AddRect(vh, r, 0.20f, 0.08f, 0.80f, 0.92f, color);
        Color groove = new Color(0.04f, 0.06f, 0.08f, color.a * 0.75f);
        for (int i = 0; i < 3; i++)
        {
            float x = 0.32f + i * 0.14f;
            AddRect(vh, r, x, 0.18f, x + 0.055f, 0.78f, groove);
        }
        AddRect(vh, r, 0.20f, 0.08f, 0.80f, 0.20f, new Color(color.r * 0.65f, color.g * 0.65f, color.b * 0.65f, color.a));
    }

    static void AddRect(VertexHelper vh, Rect r, float x0, float y0, float x1, float y1, Color c)
    {
        int start = vh.currentVertCount;
        vh.AddVert(new Vector3(r.xMin + r.width * x0, r.yMin + r.height * y0), c, Vector2.zero);
        vh.AddVert(new Vector3(r.xMin + r.width * x0, r.yMin + r.height * y1), c, Vector2.up);
        vh.AddVert(new Vector3(r.xMin + r.width * x1, r.yMin + r.height * y1), c, Vector2.one);
        vh.AddVert(new Vector3(r.xMin + r.width * x1, r.yMin + r.height * y0), c, Vector2.right);
        vh.AddTriangle(start, start + 1, start + 2);
        vh.AddTriangle(start, start + 2, start + 3);
    }

    static void AddQuad(VertexHelper vh, Rect r, Vector2 a, Vector2 b, Vector2 c, Vector2 d, Color color)
    {
        int start = vh.currentVertCount;
        vh.AddVert(ToRect(r, a), color, Vector2.zero);
        vh.AddVert(ToRect(r, b), color, Vector2.right);
        vh.AddVert(ToRect(r, c), color, Vector2.one);
        vh.AddVert(ToRect(r, d), color, Vector2.up);
        vh.AddTriangle(start, start + 1, start + 2);
        vh.AddTriangle(start, start + 2, start + 3);
    }

    static Vector3 ToRect(Rect r, Vector2 point) =>
        new(r.xMin + r.width * point.x, r.yMin + r.height * point.y);
}
