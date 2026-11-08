using Godot;

namespace Mori;

// Камера вращается вокруг героя. Её направление также определяет направление WASD.
public partial class ThirdPersonCamera : Node3D
{
    public float Yaw { get; set; }
    public float Pitch { get; set; } = .22f;
    public float Distance { get; set; } = 6.5f;
    public Camera3D View { get; private set; } = null!;
    private readonly SphereShape3D clearance = new() { Radius = .34f };
    private Vector3 focus;
    private float cameraDistance;
    private bool tracking;
    public override void _Ready()
    {
        View = new Camera3D { Current = true, Fov = 65, Near = .15f, Far = 200 };
        AddChild(View);
    }
    public Vector3 RelativeDirection(Vector2 input) =>
        new Vector3(input.X, 0, input.Y).Rotated(Vector3.Up, Yaw).Normalized();

    public void Follow(Vector3 target, float dt, bool snap = false)
    {
        Vector3 desired = target + Vector3.Up * 1.16f;
        bool cut=snap || !tracking || focus.DistanceTo(desired)>5;
        focus=cut ? desired : focus.Lerp(desired,1-Mathf.Exp(-17*dt));
        tracking=true;
        Vector3 offset = new Vector3(0, Mathf.Sin(Pitch), Mathf.Cos(Pitch)).Rotated(Vector3.Up, Yaw) * Distance;
        var sweep = new PhysicsShapeQueryParameters3D
        {
            Shape = clearance, CollisionMask = 1,
            Transform = new Transform3D(Basis.Identity, focus), Motion = offset
        };
        var safe = GetWorld3D().DirectSpaceState.CastMotion(sweep);
        float distance = Mathf.Max(.06f, Distance * safe[0] - .04f);
        cameraDistance=cut || distance<cameraDistance ? distance :
            Mathf.Lerp(cameraDistance,distance,1-Mathf.Exp(-13*dt));
        View.GlobalPosition = focus + offset.Normalized()*cameraDistance;
        View.LookAt(focus);
    }
    public void Orbit(Vector2 motion)
    {
        Yaw -= motion.X * .004f;
        Pitch = Mathf.Clamp(Pitch + motion.Y * .003f, -.08f, .7f);
    }
}
