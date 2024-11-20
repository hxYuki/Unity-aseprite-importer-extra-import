using System;
using UnityEngine;

namespace Assets.Extras.ShapeAnimation
{
    public enum ShapeType
    {
        Rectangle,
        Point
    }
    public class ShapeAnimation : ScriptableObject
    {
        public string ClipName;
        public ShapeType ShapeType;
        public bool Loop;
        public Vector2[] Points;
        public Rect[] Rects;
        public float[] Duration;

        public bool IsEmpty => Rects.Length == 0 && Points.Length == 0;

        public Rect GetI(int i, Rect _)
        {
            if (ShapeType == ShapeType.Rectangle)
                return Rects[i];

            throw new Exception("Shape type mismatch, getting Rectangle over non-Rectangle ShapeAnimation");
        }
        public Vector2 GetI(int i, Vector2 _)
        {
            if (ShapeType == ShapeType.Point)
                return Points[i];

            throw new Exception("Shape type mismatch, getting Point over non-Point ShapeAnimation");
        }
    }
}
