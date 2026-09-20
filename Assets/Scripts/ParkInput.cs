using UnityEngine;
using UnityEngine.InputSystem;

/// 観賞アプリの操作定義（キーボード＋ゲームパッド）。バインドはここ1か所だけ。
/// `<Gamepad>` は X-Input / DualShock / Switch Pro 共通。汎用ジョイスティックが要るなら AddBinding を1行足す。
public static class ParkInput
{
    static readonly InputAction demo = Make("<Keyboard>/c", "<Gamepad>/buttonWest");
    static readonly InputAction nextShot = Make("<Keyboard>/v", "<Gamepad>/rightTrigger");
    static readonly InputAction nextBall = Make("<Keyboard>/tab", "<Gamepad>/rightShoulder");
    static readonly InputAction prevBall = Make(null, "<Gamepad>/leftShoulder");   // キーボードは Shift+Tab（BallStep で判定）
    static readonly InputAction overview = Make("<Keyboard>/0", "<Gamepad>/leftTrigger");
    static readonly InputAction help = Make("<Keyboard>/h", "<Gamepad>/select");
    static readonly InputAction audio = Make("<Keyboard>/m", "<Gamepad>/buttonNorth");
    static readonly InputAction quit = Make("<Keyboard>/escape", "<Gamepad>/start", "hold(duration=1)");   // 誤爆防止の1秒長押し

    /// 最後に触ったのがゲームパッドか（ヘルプ表記の切替用）
    public static bool UsingGamepad { get; private set; }

    public static bool Demo => Pressed(demo);
    public static bool NextShot => Pressed(nextShot);
    public static bool Overview => Pressed(overview);
    public static bool Help => Pressed(help);
    public static bool Audio => Pressed(audio);
    public static bool Quit => quit.WasPerformedThisFrame();
    /// +1=次の球 / -1=前の球 / 0=操作なし
    public static int BallStep
    {
        get
        {
            if (Pressed(prevBall)) return -1;
            if (!Pressed(nextBall)) return 0;
            var kb = Keyboard.current;
            return !UsingGamepad && kb != null && kb.shiftKey.isPressed ? -1 : 1;
        }
    }

    public static string HelpText => (UsingGamepad
        ? "X Demo\nRT Next shot\nLB / RB Ball\nLT Overview\nY Audio\nSelect Help\nHold Start Quit"
        : "C Demo\nV Next shot\nTab / Shift+Tab Ball\n0 Overview\nM Audio\nH Help\nHold Esc Quit").Replace("Audio", "Audio: " + ParkAudio.Current);

    static InputAction Make(string key, string pad, string interactions = null)
    {
        var a = new InputAction(type: InputActionType.Button);
        if (key != null) a.AddBinding(key, interactions);
        a.AddBinding(pad, interactions);
        a.Enable();
        return a;
    }

    static bool Pressed(InputAction a)
    {
        if (!a.WasPressedThisFrame()) return false;
        UsingGamepad = a.activeControl?.device is Gamepad;
        return true;
    }
}
