using System.Collections.Generic;

namespace Epacenur.Core
{
    // There are no art assets: items, monsters, trees and so on describe how they look as plain data,
    // and the game builds it from Unity's primitive shapes (Runtime: ModelBuilder, IconPainter).

    public enum Shape { Cube, Sphere, Cylinder, Capsule }

    /// One primitive shape of a model: position, size (Unity primitive scale), rotation (Euler degrees), colour.
    public struct Part
    {
        public Shape Shape;
        public Vec3 Position, Scale, Rotation;
        public Rgb Colour;
        public bool Glow;  // lights itself (fire, a furnace's mouth)
    }

    /// A model placed inside another, moved, turned (Euler degrees) and scaled as a group.
    /// A named child can be found by the game, e.g. a tree's "crown" shakes when chopped.
    public struct ModelChild
    {
        public Model Model;
        public Vec3 Position, Rotation;
        public float Scale;
        public string Name;
    }

    /// A 3D model made of primitive shapes. Built fluently: new Model().Cube(...).Sphere(...).
    public sealed class Model
    {
        public readonly List<Part> Parts = new List<Part>();
        public readonly List<ModelChild> Children = new List<ModelChild>();

        /// Puts another model inside this one, moved and turned as a group.
        public Model Child(Model model, float x, float y, float z, float rx = 0f, float ry = 0f, float rz = 0f, float scale = 1f, string name = null)
        {
            Children.Add(new ModelChild { Model = model, Position = new Vec3(x, y, z), Rotation = new Vec3(rx, ry, rz), Scale = scale, Name = name });
            return this;
        }

        public Model Add(Shape shape, Vec3 position, Vec3 scale, Rgb colour, Vec3 rotation = default(Vec3), bool glow = false)
        {
            Parts.Add(new Part { Shape = shape, Position = position, Scale = scale, Colour = colour, Rotation = rotation, Glow = glow });
            return this;
        }

        public Model Cube(float x, float y, float z, float sx, float sy, float sz, Rgb c) => Add(Shape.Cube, new Vec3(x, y, z), new Vec3(sx, sy, sz), c);
        public Model Sphere(float x, float y, float z, float sx, float sy, float sz, Rgb c) => Add(Shape.Sphere, new Vec3(x, y, z), new Vec3(sx, sy, sz), c);
        public Model Cylinder(float x, float y, float z, float sx, float sy, float sz, Rgb c) => Add(Shape.Cylinder, new Vec3(x, y, z), new Vec3(sx, sy, sz), c);
        public Model Capsule(float x, float y, float z, float sx, float sy, float sz, Rgb c) => Add(Shape.Capsule, new Vec3(x, y, z), new Vec3(sx, sy, sz), c);

        /// Turns the last part added (Euler degrees).
        public Model Turn(float rx, float ry, float rz)
        {
            var p = Parts[Parts.Count - 1];
            p.Rotation = new Vec3(rx, ry, rz);
            Parts[Parts.Count - 1] = p;
            return this;
        }

        /// Makes the last part glow.
        public Model Glowing()
        {
            var p = Parts[Parts.Count - 1];
            p.Glow = true;
            Parts[Parts.Count - 1] = p;
            return this;
        }

        /// Adds every part (and child) of another model, moved by `offset`.
        public Model Include(Model other, Vec3 offset = default(Vec3))
        {
            foreach (var p in other.Parts)
            {
                var q = p;
                q.Position = p.Position + offset;
                Parts.Add(q);
            }
            foreach (var c in other.Children)
            {
                var d = c;
                d.Position = c.Position + offset;
                Children.Add(d);
            }
            return this;
        }
    }

    /// One flat shape of an icon, in the icon's own units (0..Size, y down).
    public struct IconShape
    {
        public float X, Y, W, H;
        public Rgb Colour;
        public float Angle;            // degrees, clockwise, about (PivotX, PivotY)
        public float PivotX, PivotY;
        public bool Diamond;           // a square turned 45 degrees, centred on (X, Y) with radius W
    }

    /// A small flat picture made of rectangles: inventory items (Size 40), skills (Size 22).
    public sealed class Icon
    {
        public readonly float Size;
        public readonly List<IconShape> Shapes = new List<IconShape>();

        public Icon(float size = 40f) { Size = size; }

        public Icon Rect(float x, float y, float w, float h, Rgb c)
        {
            Shapes.Add(new IconShape { X = x, Y = y, W = w, H = h, Colour = c });
            return this;
        }

        /// A rectangle turned `angle` degrees clockwise about the icon's centre.
        public Icon Turned(float x, float y, float w, float h, Rgb c, float angle) => TurnedAbout(x, y, w, h, c, angle, Size / 2f, Size / 2f);

        public Icon TurnedAbout(float x, float y, float w, float h, Rgb c, float angle, float px, float py)
        {
            Shapes.Add(new IconShape { X = x, Y = y, W = w, H = h, Colour = c, Angle = angle, PivotX = px, PivotY = py });
            return this;
        }

        public Icon Diamond(float cx, float cy, float radius, Rgb c)
        {
            Shapes.Add(new IconShape { X = cx, Y = cy, W = radius, Colour = c, Diamond = true });
            return this;
        }
    }
}
