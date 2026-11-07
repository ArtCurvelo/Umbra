using Godot;
using Umbra.Domain.Gameplay;

namespace Umbra.Game;

/// <summary>
/// HUD drawn in one Control: objective, loot log, health and Fúria bars, three skill slots (prompt for the device in use,
/// cost, cooldown sweep) and, over the world, enemy health bars, damage numbers and drop names. Base layout 1280x720;
/// the project's canvas_items stretch scales it to 1080p and 1440p.
/// </summary>
public partial class Hud : CanvasLayer
{
    private sealed record Floater(Vector3 World, string Text, Color Color, float Born);
    private sealed record Toast(Item Item, float Born);

    private readonly List<Floater> _floaters = new();
    private readonly List<Toast> _toasts = new();
    private Simulation _sim = null!;
    private CameraRig _camera = null!;
    private ActorLayer _actors = null!;
    // One canvas per part: each is redrawn only when what it shows changes (text drawing is the HUD's cost).
    private readonly Canvas _overlay = new() { Part = "mundo" }, _objective = new() { Part = "objetivo" },
                            _loot = new() { Part = "itens" }, _bottom = new() { Part = "painel" }, _banner = new() { Part = "faixa" };
    private readonly Dictionary<string, string> _signatures = new();
    private float _time;

    public string ObjectiveText { get; private set; } = "";
    public string Banner { get; private set; } = "";

    private partial class Canvas : Control
    {
        public Hud Host = null!;
        public string Part = "";
        public override void _Draw()
        {
            if (Host._sim == null) return;
            double t = Profiler.Now;
            switch (Part)
            {
                case "mundo": Host.DrawWorldOverlay(this); break;
                case "objetivo": Host.DrawObjective(this); break;
                case "itens": Host.DrawLoot(this, Size); break;
                case "painel": Host.DrawBottom(this, Size); break;
                default: Host.DrawBanner(this, Size); break;
            }
            Profiler.Add("hud", t);
        }
    }

    public override void _Ready()
    {
        foreach (var c in new[] { _overlay, _objective, _loot, _bottom, _banner })
        {
            c.Host = this;
            c.SetAnchorsPreset(Control.LayoutPreset.FullRect);
            c.MouseFilter = Control.MouseFilterEnum.Ignore;
            AddChild(c);
        }
    }

    public void Bind(Simulation sim, CameraRig camera, ActorLayer actors)
    {
        _sim = sim; _camera = camera; _actors = actors;
        _floaters.Clear(); _toasts.Clear(); Banner = "";
    }

    public void OnEvent(GameEvent e)
    {
        switch (e)
        {
            case HitEvent hit:
                _floaters.Add(new Floater(ActorLayer.World(hit.Position, 2.1f), hit.Amount.ToString(),
                                          hit.OnHero ? Materials.Rgb(255, 90, 80) : Materials.Bone, _time));
                break;
            case PickedUpEvent picked: _toasts.Add(new Toast(picked.Drop.Item, _time)); break;
            case HeroDiedEvent: Banner = "dead"; break;
        }
    }

    public override void _Process(double delta)
    {
        _time += (float)delta;
        _floaters.RemoveAll(f => _time - f.Born > .9f);
        if (_toasts.Count > 5) _toasts.RemoveAt(0);
        if (_sim != null)
        {
            ObjectiveText = _sim.Cleared
                ? (_sim.Drops.Count > 0 ? "A cripta está em silêncio. Recolha o que restou." : "Cripta limpa.")
                : $"{SliceData.BruteName}: {_sim.EnemiesRemaining} de {_sim.Enemies.Count}";
            if (_sim.Cleared && _sim.Drops.Count == 0 && Banner == "") Banner = "clear";
        }
        _overlay.QueueRedraw();
        if (_sim == null) return;
        var h = _sim.Hero;
        string size = _bottom.Size.ToString();
        Redraw(_objective, ObjectiveText + size);
        Redraw(_loot, _toasts.Count + "|" + string.Join(',', _toasts.Select(t => (int)Math.Min(90, (_time - t.Born) * 10) is var a && a >= 70 ? a : 0)) + size);
        Redraw(_bottom, $"{h.Health}|{h.Fury}|{h.Cooldown(SliceData.Investida.Id) / 6}|{DeviceTracker.Current}|{size}");
        Redraw(_banner, Banner + DeviceTracker.Current + size);
    }

    private void Redraw(Canvas canvas, string signature)
    {
        if (_signatures.TryGetValue(canvas.Part, out var old) && old == signature) return;
        _signatures[canvas.Part] = signature;
        canvas.QueueRedraw();
    }

    private Vector2 Screen(Vector3 world) => _camera.Camera.UnprojectPosition(world);
    private bool OnScreen(Vector3 world) => !_camera.Camera.IsPositionBehind(world);

    private void DrawWorldOverlay(Control c)
    {
        foreach (var e in _sim.Enemies)
        {
            if (!e.Alive || e.Health == e.Stats.MaxHealth) continue;
            var w = _actors.Interpolated(e, 1) + new Vector3(0, 2.25f, 0);
            if (!OnScreen(w)) continue;
            var p = Screen(w);
            var r = new Rect2(p - new Vector2(26, 4), new Vector2(52, 6));
            c.DrawRect(r.Grow(2), Materials.IronDark);
            c.DrawRect(new Rect2(r.Position, new Vector2(r.Size.X * e.Health / e.Stats.MaxHealth, r.Size.Y)), Materials.Blood);
        }
        foreach (var d in _sim.Drops)
        {
            var w = ActorLayer.World(d.Position, .55f);
            if (!OnScreen(w)) continue;
            var p = Screen(w);
            string label = $"[{d.Item.Tag}] {d.Item.Name}";
            float width = UiKit.Font.GetStringSize(label, HorizontalAlignment.Left, -1, 14).X + 14;
            var r = new Rect2(p - new Vector2(width / 2, 26), new Vector2(width, 22));
            c.DrawRect(r, new Color(0, 0, 0, .72f));
            c.DrawRect(r, Materials.Rarity(d.Item.Rarity), false, 1);
            UiKit.Text(c, label, new Vector2(r.Position.X, r.Position.Y + 16), 14, Materials.Rarity(d.Item.Rarity), HorizontalAlignment.Center, width);
        }
        foreach (var f in _floaters)
        {
            float age = _time - f.Born;
            var w = f.World + new Vector3(0, age * 1.1f, 0);
            if (!OnScreen(w)) continue;
            var p = Screen(w);
            UiKit.Text(c, f.Text, p - new Vector2(30, 0), age < .1f ? 24 : 20, new Color(f.Color, 1 - Mathf.Max(0, age - .5f) / .4f), HorizontalAlignment.Center, 60);
        }
    }

    private void DrawObjective(Control c)
    {
        var r = new Rect2(16, 16, 330, 62);
        UiKit.Frame(c, r);
        UiKit.Text(c, "CRIPTA ESQUECIDA", new Vector2(30, 40), 16, Materials.Gold);
        UiKit.Text(c, ObjectiveText, new Vector2(30, 64), 14, Materials.Bone);
    }

    private void DrawLoot(Control c, Vector2 size)
    {
        float y = 20;
        foreach (var t in _toasts.AsEnumerable().Reverse())
        {
            float age = _time - t.Born;
            if (age > 8) continue;
            var color = new Color(Materials.Rarity(t.Item.Rarity), Mathf.Clamp(8 - age, 0, 1));
            string text = $"[{t.Item.Tag}] {t.Item.Name} — {t.Item.RarityName}";
            float width = UiKit.Font.GetStringSize(text, HorizontalAlignment.Left, -1, 14).X + 24;
            var r = new Rect2(size.X - width - 16, y, width, 26);
            c.DrawRect(r, new Color(UiKit.Fill, .85f * color.A));
            c.DrawRect(new Rect2(r.Position, new Vector2(4, r.Size.Y)), color);
            UiKit.Text(c, text, new Vector2(r.Position.X + 12, y + 18), 14, color);
            y += 30;
        }
    }

    private void DrawBottom(Control c, Vector2 size)
    {
        var hero = _sim.Hero;
        var panel = new Rect2(size.X / 2 - 300, size.Y - 112, 600, 96);
        UiKit.Frame(c, panel);
        var bars = new Rect2(panel.Position + new Vector2(18, 18), new Vector2(300, 24));
        UiKit.Bar(c, bars, hero.Health, hero.Stats.MaxHealth, Materials.Blood, $"VIDA  {hero.Health}/{hero.Stats.MaxHealth}");
        UiKit.Bar(c, new Rect2(bars.Position + new Vector2(0, 38), bars.Size), hero.Fury, SliceData.FuryMax, Materials.Ember, $"FÚRIA  {hero.Fury}/{SliceData.FuryMax}");

        var device = DeviceTracker.Current;
        var slots = new[]
        {
            (SliceData.GolpeBrutal, "M1", "A"),
            (SliceData.Ruptura, "M2", "X"),
            (SliceData.Investida, "E", "Y"),
        };
        for (int i = 0; i < slots.Length; i++)
        {
            var (skill, key, pad) = slots[i];
            var r = new Rect2(panel.Position + new Vector2(340 + i * 84, 12), new Vector2(64, 64));
            int cd = hero.Cooldown(skill.Id);
            bool ready = cd == 0 && hero.Fury >= skill.Cost;
            c.DrawRect(r.Grow(3), Materials.IronDark);
            c.DrawRect(r, Materials.Rgb(26, 23, 28));
            Icon(c, r, skill, ready);
            if (cd > 0)
            {
                float share = cd / (float)skill.CooldownTicks;
                c.DrawRect(new Rect2(r.Position, new Vector2(r.Size.X, r.Size.Y * share)), new Color(0, 0, 0, .66f));
                UiKit.Text(c, $"{cd / 60f:0.0}", new Vector2(r.Position.X, r.Position.Y + 40), 18, Materials.Bone, HorizontalAlignment.Center, r.Size.X);
            }
            else if (!ready) c.DrawRect(r, new Color(.1f, 0, 0, .45f));
            c.DrawRect(r, ready ? Materials.Gold : Materials.Iron, false, 2);
            if (skill.Cost > 0) UiKit.Text(c, skill.Cost.ToString(), r.Position + new Vector2(44, 60), 12, Materials.Ember);
            UiKit.Prompt(c, new Vector2(r.Position.X + r.Size.X - 4, r.Position.Y + 4), device, key, pad, 10);
            UiKit.Text(c, skill.Name, new Vector2(r.Position.X - 10, r.End.Y + 14), 11, Materials.Ash, HorizontalAlignment.Center, r.Size.X + 20);
        }
    }

    /// <summary>Placeholder skill glyphs drawn with lines (final icons belong to the art phase).</summary>
    private static void Icon(Control c, Rect2 r, SkillDef skill, bool ready)
    {
        var col = ready ? Materials.Bone : Materials.Ash;
        var m = r.GetCenter();
        switch (skill.Kind)
        {
            case SkillKind.Strike:
                c.DrawLine(m + new Vector2(-16, 16), m + new Vector2(14, -14), col, 4);
                c.DrawLine(m + new Vector2(6, -18), m + new Vector2(18, -6), col, 4);
                break;
            case SkillKind.Cone:
                c.DrawPolyline(new[] { m + new Vector2(-18, 14), m + new Vector2(18, -4), m + new Vector2(18, 20), m + new Vector2(-18, 14) }, Materials.Ember, 2);
                c.DrawLine(m + new Vector2(-10, 12), m + new Vector2(10, 4), col, 2);
                c.DrawLine(m + new Vector2(0, 9), m + new Vector2(12, 14), col, 2);
                break;
            default:
                c.DrawLine(m + new Vector2(-18, 0), m + new Vector2(12, 0), col, 4);
                c.DrawPolyline(new[] { m + new Vector2(4, -10), m + new Vector2(16, 0), m + new Vector2(4, 10) }, col, 4);
                c.DrawLine(m + new Vector2(-18, -8), m + new Vector2(-6, -8), col, 2);
                c.DrawLine(m + new Vector2(-18, 8), m + new Vector2(-6, 8), col, 2);
                break;
        }
    }

    private void DrawBanner(Control c, Vector2 size)
    {
        if (Banner == "") return;
        bool dead = Banner == "dead";
        var r = new Rect2(size.X / 2 - 220, size.Y * .32f, 440, 92);
        UiKit.Frame(c, r, dead ? Materials.Blood : Materials.Gold);
        UiKit.Text(c, dead ? "VOCÊ CAIU" : "CRIPTA LIMPA", new Vector2(r.Position.X, r.Position.Y + 42), 28, dead ? Materials.Rgb(230, 90, 80) : Materials.Gold,
                   HorizontalAlignment.Center, r.Size.X);
        string hint = DeviceTracker.Current == Device.Keyboard ? "Enter: recomeçar" : "A / Cruz: recomeçar";
        UiKit.Text(c, hint, new Vector2(r.Position.X, r.Position.Y + 72), 14, Materials.Ash, HorizontalAlignment.Center, r.Size.X);
    }
}
