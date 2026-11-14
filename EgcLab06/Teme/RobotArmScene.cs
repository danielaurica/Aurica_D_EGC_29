// Varianta A: bratul robotic

namespace Egc.Teme.VariantaA;

using OpenTK.Mathematics;
using OpenTK.Windowing.GraphicsLibraryFramework;

public sealed class RobotArmScene : Scene
{
    private const string BaseLeft = "roteste baza spre stanga";
    private const string BaseRight = "roteste baza spre dreapta";
    private const string ShoulderUp = "inclina umarul intr-un sens";
    private const string ShoulderDown = "inclină umarul in sens opus";
    private const string ElbowUp = "indoaie cotul într-un sens";
    private const string ElbowDown = "indoaie cotul în sens opus";
    private const string ToggleGrip = "deschide / onchide clestele";
    private const string SavePose = "salveaza poza curenta";
    private const string PlayPoses = "porneste / opreste redarea pozelor";
    private const string ToggleEase = "schimba easing-ul redarii";
    private const string ClearPoses = "sterge pozele";

    private const float AngularSpeed = 60.0f;
    private const float ShoulderLimit = 70.0f;
    private const float ElbowLimit = 125.0f;
    private const float UpperLength = 2.5f, LowerLength = 2.0f;
    private const float ClosedGrip = 0.14f, OpenGrip = 0.45f;
    private readonly KeyframeTrack _poses = new(1.5f) { Ease = Easing.EaseInOut };
    private readonly Tween _grip = new(new Vector3(OpenGrip, 0, 0),
                                      new Vector3(OpenGrip, 0, 0), 0.3f, Easing.EaseInOut);
    private SceneObject _base = null!, _shoulder = null!, _elbow = null!;
    private SceneObject _leftFinger = null!, _rightFinger = null!;
    private Vector3 _angles = new(0.0f, -25.0f, -45.0f);
    private float _playTime;
    private bool _playing, _gripOpen = true;

    public override string Name => "tema A — braț robotic";

    public override string Status =>
        $"{(_playing ? "redare" : "manual")} | poze: {_poses.Count} | " +
        $"clește: {(_grip.IsFinished ? (_gripOpen ? "deschis" : "închis") : "în mișcare")} | " +
        $"easing: {(_poses.Ease == Easing.Linear ? "Linear" : "EaseInOut")}";

    public override IEnumerable<string> ActionNames => new[]
    {
        BaseLeft, BaseRight, ShoulderUp, ShoulderDown, ElbowUp, ElbowDown,
        ToggleGrip, SavePose, PlayPoses, ToggleEase, ClearPoses,
    };

    public override void Load(Input input)
    {
        CameraPosition = new Vector3(9.0f, 6.5f, 13.0f);
        CameraTarget = new Vector3(0.0f, 2.5f, 0.0f);

        input.Bind(BaseLeft, Keys.A);
        input.Bind(BaseRight, Keys.D);
        input.Bind(ShoulderUp, Keys.W);
        input.Bind(ShoulderDown, Keys.S);
        input.Bind(ElbowUp, Keys.Up);
        input.Bind(ElbowDown, Keys.Down);
        input.Bind(ToggleGrip, Keys.Space);
        input.Bind(SavePose, Keys.K);
        input.Bind(PlayPoses, Keys.Enter);
        input.Bind(ToggleEase, Keys.E);
        input.Bind(ClearPoses, Keys.Backspace);

        Mesh cube = AddMesh(Shapes.ShadedCube());
        Mesh sphere = AddMesh(Shapes.ShadedSphere(1.0f, 24, 12));
        Mesh cylinder = AddMesh(Shapes.ShadedCylinder(1.0f, 1.0f, 32));
        AddObject("podea", AddMesh(Shapes.Grid(8, 1.0f, new Color4(0.3f, 0.33f, 0.38f, 1.0f))));

        Color4 metal = new(0.45f, 0.5f, 0.6f, 1.0f);
        Color4 orange = new(1.0f, 0.6f, 0.15f, 1.0f);
        Color4 blue = new(0.25f, 0.55f, 1.0f, 1.0f);

        SceneObject stand = AddObject("suport", cylinder);
        stand.Transform.Position = new Vector3(0, 0.25f, 0);
        stand.Transform.Scale = new Vector3(1.2f, 0.5f, 1.2f);
        stand.Tint = metal;

        _base = AddObject("pivot bază", null);
        _base.Transform.Position = new Vector3(0, 0.5f, 0);
        SceneObject turntable = AddObject("bază rotativă", cylinder, _base);
        turntable.Transform.Position = new Vector3(0, 0.3f, 0);
        turntable.Transform.Scale = new Vector3(0.75f, 0.6f, 0.75f);
        turntable.Tint = blue;

        _shoulder = AddObject("pivot umăr", null, _base);
        _shoulder.Transform.Position = new Vector3(0, 0.6f, 0);
        SceneObject shoulderJoint = AddObject("articulație umăr", sphere, _shoulder);
        shoulderJoint.Transform.Scale = new Vector3(0.35f);
        shoulderJoint.Tint = metal;

        SceneObject upper = AddObject("segment superior", cube, _shoulder);
        upper.Transform.Position = new Vector3(0, UpperLength / 2, 0);
        upper.Transform.Scale = new Vector3(0.45f, UpperLength, 0.45f);
        upper.Tint = orange;

        _elbow = AddObject("pivot cot", null, _shoulder);
        _elbow.Transform.Position = new Vector3(0, UpperLength, 0);
        SceneObject elbowJoint = AddObject("articulație cot", sphere, _elbow);
        elbowJoint.Transform.Scale = new Vector3(0.3f);
        elbowJoint.Tint = metal;

        SceneObject lower = AddObject("antebraț", cube, _elbow);
        lower.Transform.Position = new Vector3(0, LowerLength / 2, 0);
        lower.Transform.Scale = new Vector3(0.35f, LowerLength, 0.35f);
        lower.Tint = blue;

        SceneObject wrist = AddObject("pivot clește", null, _elbow);
        wrist.Transform.Position = new Vector3(0, LowerLength, 0);
        SceneObject palm = AddObject("palmă", cube, wrist);
        palm.Transform.Scale = new Vector3(1.1f, 0.25f, 0.5f);
        palm.Tint = metal;
        _leftFinger = AddObject("deget stâng", cube, wrist);
        _rightFinger = AddObject("deget drept", cube, wrist);
        foreach (SceneObject finger in new[] { _leftFinger, _rightFinger })
        {
            finger.Transform.Scale = new Vector3(0.16f, 0.7f, 0.35f);
            finger.Tint = orange;
        }
        ApplyPose();
    }

    public override void Update(float dt, Input input)
    {
        if (dt <= 0.0f) return;

        if (input.WasPressed(ClearPoses))
        {
            _poses.Clear();
            _playing = false;
            _playTime = 0.0f;
        }
        if (input.WasPressed(SavePose)) _poses.Add(_angles);
        if (input.WasPressed(PlayPoses) && _poses.Count > 0)
        {
            _playing = !_playing;
            _playTime = 0.0f;
        }
        if (input.WasPressed(ToggleEase))
            _poses.Ease = _poses.Ease == Easing.Linear ? Easing.EaseInOut : Easing.Linear;

        if (_playing)
        {
            _angles = _poses.Sample(_playTime);
            _playTime = (_playTime + dt) % _poses.Duration;
        }
        else
        {
            _angles.X += input.Axis(BaseLeft, BaseRight) * AngularSpeed * dt;
            _angles.Y = Math.Clamp(_angles.Y + input.Axis(ShoulderDown, ShoulderUp) * AngularSpeed * dt,
                                   -ShoulderLimit, ShoulderLimit);
            _angles.Z = Math.Clamp(_angles.Z + input.Axis(ElbowDown, ElbowUp) * AngularSpeed * dt,
                                   -ElbowLimit, ElbowLimit);
        }

        if (input.WasPressed(ToggleGrip))
        {
            _gripOpen = !_gripOpen;
            _grip.GoTo(new Vector3(_gripOpen ? OpenGrip : ClosedGrip, 0, 0), 0.3f);
        }
        _grip.Update(dt);
        ApplyPose();
    }

    private void ApplyPose()
    {
        _base.Transform.Rotation = new Vector3(0, _angles.X, 0);
        _shoulder.Transform.Rotation = new Vector3(0, 0, _angles.Y);
        _elbow.Transform.Rotation = new Vector3(0, 0, _angles.Z);
        float opening = _grip.Value.X;
        _leftFinger.Transform.Position = new Vector3(-opening, 0.45f, 0);
        _rightFinger.Transform.Position = new Vector3(opening, 0.45f, 0);
    }

    public override IEnumerable<string> Describe()
    {
        foreach (string line in base.Describe()) yield return line;
        yield return $"Stare         : {Status}";
        yield return $"Unghiuri      : bază {_angles.X:F1}°, umăr {_angles.Y:F1}°, cot {_angles.Z:F1}°";
        yield return $"Limite        : umăr ±{ShoulderLimit:F0}°, cot ±{ElbowLimit:F0}°";
        yield return $"Viteză        : {AngularSpeed:F0}°/s (manual)";
        yield return $"Redare        : {_poses.SecondsPerKey:F1} s între poze; t = {_playTime:F2} s";
        yield return $"Clește        : deschidere {_grip.Value.X:F2}; durată {_grip.Duration:F1} s";
    }
}
