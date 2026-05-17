using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Raylib_cs;
using VectorGraphics;

namespace PhysicsCSAlevlProject;

/// <summary>
/// The Collider class is the base class for all types of colliders
/// this is so that they can be stored in the same list and used for collision detection
/// </summary>
public abstract class Collider
{
    /// <summary>
    /// the position of the collider
    /// </summary>
    public virtual Vector2 Position { get; set; }

    /// <summary>
    /// Determines if the given point is contained within the collider and calculates the closest point on the collider to the given point.
    /// This method is used for collision detection and response, allowing the application to determine if a particle or object is colliding with the collider and to find the nearest point on the collider's surface for accurate physics interactions.
    ///  The method returns true if the point is inside the collider, and false otherwise, while also providing the closest point on the collider as an output parameter.
    /// it outputs the same point if the point is outside the collider, which is used for calculating the collision response direction and magnitude based on how far the point is from the collider's surface.
    /// </summary>
    /// <param name="point"></param>
    /// <param name="closestPoint"></param>
    /// <returns></returns>
    public abstract bool ContainsPoint(Vector2 point, out Vector2 closestPoint);

    /// <summary>
    /// Creates a deep copy of the collider,
    /// </summary>
    /// <returns></returns>
    public abstract Collider DeepCopy();

    /// <summary>
    /// Draws the collider
    /// </summary>
    public virtual void Draw() { }

    public static bool IsPointInside(Rectangle rect, Vector2 point)
    {
        return point.X >= rect.X
            && point.X <= rect.X + rect.Width
            && point.Y >= rect.Y
            && point.Y <= rect.Y + rect.Height;
    }
}

/// <summary>
/// The CircleCollider class represents a circular collider with a specified radius and position.
/// </summary>
public class CircleCollider : Collider
{
    /// <summary>
    /// the radius of the circle collider
    /// </summary>
    public float Radius;

    /// <summary>
    /// the rectangle used for the broad phase of collision detection, which is a simple bounding box that encompasses the circle collider.
    /// </summary>
    public Rectangle BroadPhase =>
        new Rectangle(
            (int)(Position.X - Radius),
            (int)(Position.Y - Radius),
            (int)(Radius * 2),
            (int)(Radius * 2)
        );

    public CircleCollider(Vector2 center, float radius)
    {
        Position = center;
        Radius = radius;
    }

    public override Collider DeepCopy() => new CircleCollider(Position, Radius);

    public override bool ContainsPoint(Vector2 point, out Vector2 closestPoint)
    {
        if (!IsPointInside(BroadPhase, point))
        {
            closestPoint = point;
            return false;
        }
        Vector2 direction = point - Position;
        float distance = direction.LengthSquared();

        if (distance <= Radius * Radius)
        {
            closestPoint = Position + Vector2.Normalize(direction) * Radius;
            return true;
        }

        closestPoint = point;
        return false;
    }

    public override void Draw()
    {
        var circle = new PrimitiveBatch.Circle(Position, Radius, Color.Red, false);
        circle.Draw();
    }
}

/// <summary>
/// The RectangleCollider class represents an axis-aligned rectangular collider defined by a Rectangle structure.
/// </summary>
/// <param name="rectangle"></param>
public class RectangleCollider(Rectangle rectangle) : Collider
{
    /// <summary>
    /// the rectangle that defines the bounds of the rectangle collider
    /// </summary>
    public Rectangle Rectangle = rectangle;

    public override Collider DeepCopy() => new RectangleCollider(Rectangle);

    /// <summary>
    /// Determines if the given point is contained within the rectangle collider and calculates the closest point on the rectangle to the given point.
    /// </summary>
    /// <param name="point"></param>
    /// <param name="closestPoint"></param>
    /// <returns></returns>
    public override bool ContainsPoint(Vector2 point, out Vector2 closestPoint)
    {
        if (IsPointInside(Rectangle, point))
        {
            float leftDist = point.X - Rectangle.X;
            float rightDist = Rectangle.X + Rectangle.Width - point.X;
            float topDist = point.Y - Rectangle.Y;
            float bottomDist = Rectangle.Y + Rectangle.Height - point.Y;

            float minDist = leftDist;
            closestPoint = new Vector2(Rectangle.X, point.Y);

            if (rightDist < minDist)
            {
                minDist = rightDist;
                closestPoint = new Vector2(Rectangle.X + Rectangle.Width, point.Y);
            }
            if (topDist < minDist)
            {
                minDist = topDist;
                closestPoint = new Vector2(point.X, Rectangle.Y);
            }
            if (bottomDist < minDist)
            {
                closestPoint = new Vector2(point.X, Rectangle.Y + Rectangle.Height);
            }
            return true;
        }
        closestPoint = point;

        return false;
    }

    public override void Draw()
    {
        var rect = new PrimitiveBatch.Rectangle(
            new Vector2(Rectangle.X, Rectangle.Y),
            new Vector2(Rectangle.Width, Rectangle.Height),
            Color.Red,
            false
        );
        rect.Draw();
    }
}

/// <summary>
/// The SeperatedAxisRectangleCollider class represents a rectangle collider that can be rotated and uses the Separating Axis Theorem (SAT) for collision detection.
/// </summary>
public class SeperatedAxisRectangleCollider : PolygonSeperatedAxisCollider
{
    /// <summary>
    /// all of the axes of the rectangle collider, which are used for collision detection using the Separating Axis Theorem (SAT)
    /// </summary>
    public Vector2[] Axis { get; private set; } = new Vector2[4];

    /// <summary>
    /// the half width of the rectangle collider used for calculating the vertices and axes of the rectangle
    /// </summary>
    public float HalfWidth;

    /// <summary>
    /// the half height of the rectangle collider used for calculating the vertices and axes of the rectangle
    /// </summary>
    public float HalfHeight;

    /// <summary>
    /// the position of the rectangle collider, which is the center point of the rectangle
    /// </summary>
    private Vector2 _position;

    /// <summary>
    /// the rectangle used for drawing the collider
    /// </summary>
    private PrimitiveBatch.Rectangle rectangleDraw;

    /// <summary>
    /// the position of the rectangle collider, which is the center point of the rectangle. When the position is set, it updates the position of the rectangle used for drawing and recalculates the vertices and axes of the rectangle based on the new position.
    /// </summary>
    public override Vector2 Position
    {
        get { return _position; }
        set
        {
            _position = value;
            if (rectangleDraw != null)
            {
                rectangleDraw = new PrimitiveBatch.Rectangle(
                    new Vector2(_position.X - HalfWidth, _position.Y - HalfHeight),
                    new Vector2(HalfWidth * 2, HalfHeight * 2),
                    Color.Red,
                    true
                )
                {
                    rotation = angle,
                };
            }
        }
    }

    /// <summary>
    /// the angle of the rectangle collider in radians, which determines the rotation of the rectangle. When the angle is set, it updates the rotation of the rectangle used for drawing and recalculates the vertices and axes of the rectangle based on the new angle.
    /// </summary>
    public float Angle
    {
        get { return angle; }
        set
        {
            angle = value;
            SetAxis();
            if (rectangleDraw != null)
            {
                rectangleDraw.rotation = angle;
            }
        }
    }

    public override Collider DeepCopy() =>
        new SeperatedAxisRectangleCollider(
            new Rectangle(
                (int)(Position.X - HalfWidth),
                (int)(Position.Y - HalfHeight),
                (int)(HalfWidth * 2),
                (int)(HalfHeight * 2)
            ),
            angle
        );

    /// <summary>
    /// recreates the axes and vertices of the rectangle collider based on the current angle and half width and half height of the rectangle.
    /// </summary>
    private void SetAxis()
    {
        float cos = MathF.Cos(angle);
        float sin = MathF.Sin(angle);

        Axis[0] = new Vector2(cos, sin);
        Axis[1] = new Vector2(-sin, cos);
        Axis[2] = -Axis[0];
        Axis[3] = -Axis[1];
        Vertices =
        [
            new Vector2(-HalfWidth, -HalfHeight),
            new Vector2(HalfWidth, -HalfHeight),
            new Vector2(HalfWidth, HalfHeight),
            new Vector2(-HalfWidth, HalfHeight),
        ];
    }

    public SeperatedAxisRectangleCollider(Rectangle rectangle, float angle)
        : base(
            [
                new Vector2(-rectangle.Width / 2f, -rectangle.Height / 2f),
                new Vector2(rectangle.Width / 2f, -rectangle.Height / 2f),
                new Vector2(rectangle.Width / 2f, rectangle.Height / 2f),
                new Vector2(-rectangle.Width / 2f, rectangle.Height / 2f),
            ]
        )
    {
        HalfWidth = rectangle.Width / 2f;
        HalfHeight = rectangle.Height / 2f;
        _position = new Vector2(
            rectangle.X + rectangle.Width / 2,
            rectangle.Y + rectangle.Height / 2
        );
        rectangleDraw = new PrimitiveBatch.Rectangle(
            new Vector2(rectangle.X, rectangle.Y),
            new Vector2(rectangle.Width, rectangle.Height),
            Color.Red,
            true
        );
        Angle = angle;
        Vertices =
        [
            new Vector2(-HalfWidth, -HalfHeight),
            new Vector2(HalfWidth, -HalfHeight),
            new Vector2(HalfWidth, HalfHeight),
            new Vector2(-HalfWidth, HalfHeight),
        ];

        SetAxis();
    }

    public override bool ContainsPoint(Vector2 point, out Vector2 closestPoint)
    {
        Vector2 localPoint = point - Position;
        float xProjection = Vector2.Dot(localPoint, Axis[0]);
        float yProjection = Vector2.Dot(localPoint, Axis[1]);

        float clampedX = Math.Clamp(xProjection, -HalfWidth, HalfWidth);
        float clampedY = Math.Clamp(yProjection, -HalfHeight, HalfHeight);

        bool isInside =
            (Math.Abs(xProjection - clampedX) < 0.001f)
            && (Math.Abs(yProjection - clampedY) < 0.001f);

        if (!isInside)
        {
            closestPoint = point;
            return false;
        }

        float dx = HalfWidth - Math.Abs(xProjection);
        float dy = HalfHeight - Math.Abs(yProjection);

        if (dx < dy)
        {
            float targetX = Math.Sign(xProjection) * HalfWidth;
            closestPoint = Position + Axis[0] * targetX + Axis[1] * yProjection;
        }
        else
        {
            float targetY = Math.Sign(yProjection) * HalfHeight;
            closestPoint = Position + Axis[0] * xProjection + Axis[1] * targetY;
        }

        return true;
    }

    public override void Draw()
    {
        rectangleDraw.Draw();
    }
}

/// <summary>
/// general polygon collider that uses the Separating Axis Theorem (SAT) for collision detection, which can represent any shape not self intersectiong such as rectangles, triangles, and more complex polygons.
/// </summary>
public class PolygonSeperatedAxisCollider : Collider
{
    /// <summary>
    /// the vertices of the polygon collider defined in local space
    /// </summary>
    public Vector2[] Vertices;

    /// <summary>
    /// the vertices of the polygon collider defined in world space
    /// </summary>
    public Vector2[] worldVertices;

    /// <summary>
    /// the triangles used for drawing the polygon collider
    /// </summary>
    private List<Vector2[]> Triangles = new();

    /// <summary>
    /// the axis of the polygon collider used for collision detection using the Separating Axis Theorem (SAT)
    /// </summary>
    private Vector2[] _axisPrivate;

    /// <summary>
    /// the position
    /// </summary>
    private Vector2 _position;

    /// <summary>
    /// the angle of the polygon collider in radians
    /// </summary>
    private float _angle;

    /// <summary>
    /// the broadphase rectangle used for a quick check to see if a point is potentially colliding with the polygon collider
    /// </summary>
    private Rectangle _localBroadPhase;

    /// <summary>
    /// the broadphase rectangle used for a quick check to see if a point is potentially colliding with the polygon collider
    /// </summary>
    private Rectangle _worldBroadPhase;

    /// <summary>
    /// a boolean flag to indicate whether the world vertices and world broadphase cache needs to be updated
    /// </summary>
    private bool _worldCacheDirty;

    /// <summary>
    /// the position of the polygon collider, which is the center point of the polygon. When the position is set, it updates the position of the polygon used for drawing and recalculates the vertices and axes of the polygon based on the new position.
    /// </summary>
    public override Vector2 Position
    {
        get { return _position; }
        set
        {
            if (_position != value)
            {
                _position = value;
                _worldCacheDirty = true;
            }
        }
    }

    /// <summary>
    /// the angle of the polygon collider in radians, which determines the rotation of the polygon. When the angle is set, it updates the rotation of the polygon used for drawing and recalculates the vertices and axes of the polygon based on the new angle.
    /// </summary>
    public float angle
    {
        get { return _angle; }
        set
        {
            _angle = value;
            if (Vertices == null || _axisPrivate == null)
                return;

            float cos = MathF.Cos(_angle);
            float sin = MathF.Sin(_angle);
            for (int i = 0; i < Vertices.Length; i++)
            {
                _axisPrivate[i] = new Vector2(
                    Vertices[i].X * cos - Vertices[i].Y * sin,
                    Vertices[i].X * sin + Vertices[i].Y * cos
                );
            }
            _worldCacheDirty = true;
        }
    }

    public override Collider DeepCopy() =>
        new PolygonSeperatedAxisCollider(Vertices.Select(v => v).ToArray())
        {
            Position = Position,
            angle = angle,
        };

    public PolygonSeperatedAxisCollider(Vector2[] vertices)
    {
        Vertices = vertices?.ToArray() ?? Array.Empty<Vector2>();
        worldVertices = new Vector2[Vertices.Length];
        _axisPrivate = new Vector2[Vertices.Length];

        _localBroadPhase = CreateBroadPhase(Vertices);
        _worldBroadPhase = _localBroadPhase;
        _worldCacheDirty = true;

        List<Vector2> vertsList = Vertices.ToList();

        while (vertsList.Count > 3)
        {
            bool foundEar = false;
            for (int i = 0; i < vertsList.Count; i++)
            {
                var A = vertsList[i];
                var B = vertsList[(i + 1) % vertsList.Count];
                var C = vertsList[(i + 2) % vertsList.Count];
                if (IsEar(A, B, C, vertsList.ToArray()))
                {
                    Triangles.Add([A, B, C]);
                    vertsList.RemoveAt((i + 1) % vertsList.Count);
                    foundEar = true;
                    break;
                }
            }
            if (!foundEar)
                break;
            Triangles.Add(vertsList.Take(3).ToArray());
        }
    }

    private static Rectangle CreateBroadPhase(Vector2[] vertices)
    {
        if (vertices == null || vertices.Length == 0)
        {
            return new Rectangle(0, 0, 0, 0);
        }

        Vector2 xmin = vertices[0];
        Vector2 xmax = vertices[0];
        foreach (var v in vertices)
        {
            if (v.X < xmin.X)
                xmin.X = v.X;
            if (v.Y < xmin.Y)
                xmin.Y = v.Y;
            if (v.X > xmax.X)
                xmax.X = v.X;
            if (v.Y > xmax.Y)
                xmax.Y = v.Y;
        }
        return new Rectangle(
            (int)xmin.X,
            (int)xmin.Y,
            (int)(xmax.X - xmin.X),
            (int)(xmax.Y - xmin.Y)
        );
    }

    private void UpdateWorldCacheIfNeeded()
    {
        if (!_worldCacheDirty)
        {
            return;
        }

        for (int i = 0; i < Vertices.Length; i++)
        {
            worldVertices[i] = Vertices[i] + Position;
        }

        _worldBroadPhase = new Rectangle(
            _localBroadPhase.X + (int)Position.X,
            _localBroadPhase.Y + (int)Position.Y,
            _localBroadPhase.Width,
            _localBroadPhase.Height
        );

        _worldCacheDirty = false;
    }

    private bool IsEar(Vector2 A, Vector2 B, Vector2 C, Vector2[] points)
    {
        var cross = (B.X - A.X) * (C.Y - B.Y) - (B.Y - A.Y) * (C.X - B.X);
        if (cross <= 0)
            return false;
        foreach (var P in points)
        {
            if (P == A || P == B || P == C)
                continue;
            if (PointInTriangle(P, A, B, C))
                return false;
        }
        return true;
    }

    private bool PointInTriangle(Vector2 P, Vector2 A, Vector2 B, Vector2 C)
    {
        var areaABC = Area(A, B, C);
        var areaPAB = Area(P, A, B);
        var areaPBC = Area(P, B, C);
        var areaPCA = Area(P, C, A);
        return Math.Abs(areaABC - (areaPAB + areaPBC + areaPCA)) < 0.01f;
    }

    private Vector2 GetClosestPointOnLineSegment(Vector2 A, Vector2 B, Vector2 P)
    {
        Vector2 AB = B - A;
        float t = Vector2.Dot(P - A, AB) / AB.LengthSquared();
        t = Math.Clamp(t, 0f, 1f);
        return A + AB * t;
    }

    private float Area(Vector2 A, Vector2 B, Vector2 C)
    {
        return Math.Abs((A.X * (B.Y - C.Y) + B.X * (C.Y - A.Y) + C.X * (A.Y - B.Y)) / 2.0f);
    }

    public override bool ContainsPoint(Vector2 point, out Vector2 closestPoint)
    {
        closestPoint = point;
        if (Vertices == null || Vertices.Length < 3)
        {
            return false;
        }

        UpdateWorldCacheIfNeeded();

        if (!IsPointInside(_worldBroadPhase, point))
        {
            return false;
        }

        bool isInside = IsPointInPolygon(point, worldVertices);

        float closestDistanceSq = float.MaxValue;
        Vector2 bestPoint = point;

        for (int i = 0; i < worldVertices.Length; i++)
        {
            Vector2 a = worldVertices[i];
            Vector2 b = worldVertices[(i + 1) % worldVertices.Length];
            Vector2 projected = GetClosestPointOnLineSegment(a, b, point);

            float distSq = Vector2.DistanceSquared(point, projected);
            if (distSq < closestDistanceSq)
            {
                closestDistanceSq = distSq;
                bestPoint = projected;
            }
        }

        closestPoint = bestPoint;
        return isInside;
    }

    private bool IsPointInPolygon(Vector2 point, Vector2[] polygon)
    {
        bool inside = false;
        for (int i = 0, j = polygon.Length - 1; i < polygon.Length; j = i++)
        {
            Vector2 pi = polygon[i];
            Vector2 pj = polygon[j];

            bool intersects =
                ((pi.Y > point.Y) != (pj.Y > point.Y))
                && (point.X < (pj.X - pi.X) * (point.Y - pi.Y) / ((pj.Y - pi.Y) + 1e-6f) + pi.X);

            if (intersects)
            {
                inside = !inside;
            }
        }

        return inside;
    }

    public override void Draw()
    {
        UpdateWorldCacheIfNeeded();

        for (int i = 0; i < worldVertices.Length; i++)
        {
            var line = new PrimitiveBatch.Line(
                worldVertices[i],
                worldVertices[(i + 1) % worldVertices.Length],
                Color.Red,
                2
            );
            line.Draw();
        }
    }
}
