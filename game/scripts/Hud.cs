using Godot;
using Umbra.Domain.Play;
using Umbra.Domain.Skills;

namespace Umbra.Game;

/// <summary>
/// HUD drawn in one Control: objective, loot log, health and resource bars, experience, three skill slots (the attack button
/// and the first two skill slots; prompt for the device in use, cost, cooldown sweep) and, over the world, enemy health bars,
/// damage numbers and drop names. Base layout 1280x720; the project's canvas_items stretch scales it to 1080p and 1440p.
/// </summary>
public partial class Hud : CanvasLayer
{
    private sealed record Floater(Vector3 World, string Text, Color Color, float Born);
    private sealed record Toast(string Text, Color Color, float Born);

    private readonly List<Floater> _floaters = new();
    private readonly List<Toast> _toasts = new();
    private GameSession _session = null!;
    private CameraRig _camera = null!;
    private ActorLayer _actors = null!;
    // One canvas per part: each is redrawn only when what it shows changes (text drawing is the HUD's cost).
    private readonly Canvas _overlay = new() { Part = "mundo" }, _objective = new() { Part = "objetivo" },
                            _loot = new() { Part = "itens" }, _bottom = new() { Part = "painel" }, _banner = new() { Part = "faixa" },
                            _boss = new() { Part = "chefe" };
    private readonly Dictionary<string, string> _signatures = new();
    private float _time;

    public static bool DamageNumbers = true, ShowFps;
    public string ObjectiveText { get; private set; } = "";
    public string Banner { get; private set; } = "";

    private partial class Canvas : Control
    {
        public Hud Host = null!;
        public string Part = "";
        public override void _Draw()
        {
            if (Host._session == null) return;
            double t = Profiler.Now;
            switch (Part)
            {
                case "mundo": Host.DrawWorldOverlay(this); break;
                case "objetivo": Host.DrawObjective(this); break;
                case "itens": Host.DrawLoot(this, Size); break;
                case "painel": Host.DrawBottom(this, Size); break;
                case "chefe": Host.DrawBoss(this, Size); break;
                default: Host.DrawBanner(this, Size); break;
            }
            Profiler.Add("hud", t);
        }
    }

    public override void _Ready()
    {
        foreach (var c in new[] { _overlay, _objective, _loot, _bottom, _boss, _banner })
        {
            c.Host = this;
            c.SetAnchorsPreset(Control.LayoutPreset.FullRect);
            c.MouseFilter = Control.MouseFilterEnum.Ignore;
            AddChild(c);
        }
    }

    public void Bind(GameSession session, CameraRig camera, ActorLayer actors)
    {
        _session = session; _camera = camera; _actors = actors;
        _floaters.Clear(); _toasts.Clear(); Banner = "";
        UpdateObjective();
    }

    private static string Label(GroundItem item) => item.Kind is "weapon" or "armor" or "accessory"
        ? $"[{ItemView.Tag(item.Data)}] {ItemView.Name(item.Data)}" : item.Kind == "gold" ? $"{item.Data.Get("value")} de ouro" : ItemView.Name(item.Data);

    public void OnEvent(PlayEvent e)
    {
        switch (e)
        {
            case DamageEvent hit when DamageNumbers:
                _floaters.Add(new Floater(ActorLayer.World(hit.Position, 2.1f), hit.Amount.ToString(),
                                          hit.OnHero ? Materials.Rgb(255, 90, 80) : Materials.Bone, _time));
                break;
            case ItemTakenEvent taken:
                string text = taken.ToBag ? $"{Label(taken.Item)} — {ItemView.Rarity(taken.Item.Data)}" : Label(taken.Item);
                _toasts.Add(new Toast(text, Materials.RarityOf(taken.Item.Data), _time)); break;
            case LevelUpEvent up: _toasts.Add(new Toast($"Nível {up.Level}: +1 ponto de habilidade", Materials.Gold, _time)); break;
            case HeroDownEvent: Banner = "dead"; break;
        }
    }

    public override void _Process(double delta)
    {
        _time += (float)delta;
        _floaters.RemoveAll(f => _time - f.Born > .9f);
        if (_toasts.Count > 5) _toasts.RemoveAt(0);
        UpdateObjective();
        _overlay.QueueRedraw();
        if (_session == null) return;
        var h = _session.Hero;
        string size = _bottom.Size.ToString();
        string cooldowns = string.Join(',', h.Skills.Cooldowns.Values.Select(v => v / 6));
        Redraw(_objective, ObjectiveText + size);
        var boss = _session.Boss; var bc = boss.Controller;
        Redraw(_boss, BossShown ? $"{boss.Health}|{boss.MaxHealth}|{bc?.Phase}|{(bc is { MessageTimer: > 0 } ? bc.Message : "")}|{size}" : "-");
        Redraw(_loot, _toasts.Count + "|" + string.Join(',', _toasts.Select(t => (int)Math.Min(90, (_time - t.Born) * 10) is var a && a >= 70 ? a : 0)) + size);
        Redraw(_bottom, $"{h.Inventory.Potion}|{h.Health}|{h.MaxHealth}|{h.Resource}|{h.MaxResource}|{h.Xp}|{h.Level}|{cooldowns}|{h.BasicSkillId}|{string.Join(',', h.EquippedMainSkills)}|{DeviceTracker.Current}|{size}");
        Redraw(_banner, Banner + DeviceTracker.Current + size + (ShowFps ? Engine.GetFramesPerSecond().ToString() : ""));
    }

    private void UpdateObjective()
    {
        if (_session == null) return;
        int total = _session.Enemies.Count(e => !e.Summoned), left = _session.Enemies.Count(e => !e.Summoned && e.Alive);
        ObjectiveText = _session.Cleared
            ? (_session.Items.Count > 0 ? "A cripta está em silêncio. Recolha o que restou." : "Cripta limpa.")
            : $"Inimigos: {left} de {total}" + (_session.Boss.Alive ? "  —  o guardião espera" : "");
        if (_session.Cleared && _session.Items.Count == 0 && Banner == "") Banner = "clear";
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
        foreach (var e in _session.Enemies)
        {
            if (!e.Alive || e.Health == e.MaxHealth) continue;
            var w = _actors.Interpolated(_session.IdOf(e), 1) + new Vector3(0, 2.25f, 0);
            if (!OnScreen(w)) continue;
            var p = Screen(w);
            var r = new Rect2(p - new Vector2(26, 4), new Vector2(52, 6));
            c.DrawRect(r.Grow(2), Materials.IronDark);
            c.DrawRect(new Rect2(r.Position, new Vector2(r.Size.X * e.Health / e.MaxHealth, r.Size.Y)), Materials.Blood);
        }
        foreach (var d in _session.Items)
        {
            var w = ActorLayer.World(GameSession.ToWorld(d.Cell), .55f);
            if (!OnScreen(w)) continue;
            var p = Screen(w);
            string label = Label(d); var color = Materials.RarityOf(d.Data);
            float width = UiKit.Measure(label, 14) + 14;
            var r = new Rect2(p - new Vector2(width / 2, 34), new Vector2(width, 30));
            c.DrawRect(r, new Color(0, 0, 0, .72f));
            c.DrawRect(r, color, false, 2);
            UiKit.TextIn(c, label, r, 14, color);
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
        float width = Mathf.Max(330, UiKit.Measure(ObjectiveText, 22) + 36);
        var r = new Rect2(16, 16, width, 78);
        UiKit.Frame(c, r);
        UiKit.TextIn(c, "CRIPTA ESQUECIDA", new Rect2(r.Position + new Vector2(0, 8), new Vector2(r.Size.X, 28)), 22, Materials.Gold, HorizontalAlignment.Left, 18);
        UiKit.TextIn(c, ObjectiveText, new Rect2(r.Position + new Vector2(0, 38), new Vector2(r.Size.X, 28)), 22, Materials.Bone, HorizontalAlignment.Left, 18);
    }

    private void DrawLoot(Control c, Vector2 size)
    {
        float y = 20;
        foreach (var t in _toasts.AsEnumerable().Reverse())
        {
            float age = _time - t.Born;
            if (age > 8) continue;
            var color = new Color(t.Color, Mathf.Clamp(8 - age, 0, 1));
            float width = UiKit.Measure(t.Text, 22) + 24;
            var r = new Rect2(size.X - width - 16, y, width, 32);
            c.DrawRect(r, new Color(UiKit.Fill, .85f * color.A));
            c.DrawRect(new Rect2(r.Position, new Vector2(4, r.Size.Y)), color);
            UiKit.TextIn(c, t.Text, r, 22, color, HorizontalAlignment.Left, 12);
            y += 36;
        }
    }

    private void DrawBottom(Control c, Vector2 size)
    {
        var hero = _session.Hero;
        var panel = new Rect2(size.X / 2 - 300, size.Y - 112, 600, 96);
        UiKit.Frame(c, panel);
        var bars = new Rect2(panel.Position + new Vector2(18, 16), new Vector2(300, 26));
        UiKit.Bar(c, bars, hero.Health, hero.MaxHealth, Materials.Blood, $"VIDA  {hero.Health}/{hero.MaxHealth}");
        UiKit.Bar(c, new Rect2(bars.Position + new Vector2(0, 38), bars.Size), hero.Resource, hero.MaxResource, Materials.Ember,
                  $"{hero.ResourceLabel}  {hero.Resource}/{hero.MaxResource}");
        // experience: a thin line along the panel's bottom edge, the level above the panel
        var xp = new Rect2(panel.Position.X + 18, panel.End.Y - 9, panel.Size.X - 36, 3);
        c.DrawRect(xp, Materials.IronDark);
        c.DrawRect(new Rect2(xp.Position, new Vector2(xp.Size.X * Mathf.Clamp(hero.Xp / (float)Math.Max(1, hero.XpToNext), 0, 1), xp.Size.Y)), Materials.Gold);
        UiKit.Text(c, $"NÍVEL {hero.Level}", new Vector2(panel.Position.X + 18, panel.Position.Y - 6), 11, Materials.Gold);
        // potions (player.use_potion: half the life), with the device's prompt
        var potionKey = new Vector2(panel.Position.X + 300, panel.Position.Y - 10);
        UiKit.Prompt(c, potionKey, DeviceTracker.Current, "R", "Esq.", 10);   // D-pad left
        UiKit.Text(c, $"POÇÕES {hero.Inventory.Potion}", new Vector2(potionKey.X - 18, panel.Position.Y - 6), 11,
                   hero.Inventory.Potion > 0 ? Materials.Bone : Umbra.Game.Ui.Parts.Faint, HorizontalAlignment.Right, 120);

        var device = DeviceTracker.Current;
        var keys = new[] { ("M1", "A"), ("M2", "X"), ("E", "Y") };
        for (int i = 0; i < keys.Length; i++)
        {
            var (key, pad) = keys[i];
            var skill = _session.Slot(i);
            var r = new Rect2(panel.Position + new Vector2(340 + i * 84, 12), new Vector2(64, 64));
            c.DrawRect(r.Grow(3), Materials.IronDark);
            c.DrawRect(r, Materials.Rgb(26, 23, 28));
            UiKit.Prompt(c, new Vector2(r.Position.X + r.Size.X - 4, r.Position.Y + 4), device, key, pad, 10);
            if (skill == null) { c.DrawRect(r, Materials.Iron, false, 2); continue; }
            string id = skill.Str("id");
            int cd = hero.Skills.Cooldowns.GetValueOrDefault(id); long cost = (long)skill.Num("cost", 0); double total = skill.Num("cooldown", 0);
            bool ready = cd == 0 && hero.Resource >= cost;
            Icon(c, r, skill.Str("shape"), ready);
            if (cd > 0 && total > 0)
            {
                float share = (float)Math.Min(1, cd / total);
                c.DrawRect(new Rect2(r.Position, new Vector2(r.Size.X, r.Size.Y * share)), new Color(0, 0, 0, .66f));
                UiKit.TextIn(c, $"{cd / 60f:0.0}", r, 22, Materials.Bone);
            }
            else if (!ready) c.DrawRect(r, new Color(.1f, 0, 0, .45f));
            c.DrawRect(r, ready ? Materials.Gold : Materials.Iron, false, 2);
            if (cost > 0) UiKit.Text(c, cost.ToString(), r.Position + new Vector2(46, 60), 11, Materials.Ember);
            UiKit.Text(c, skill.Str("name"), new Vector2(r.Position.X - 10, r.End.Y + 14), 11, Materials.Ash, HorizontalAlignment.Center, r.Size.X + 20);
        }
    }

    /// <summary>Placeholder skill glyphs drawn with lines, by the skill's shape (final icons belong to the art phase).</summary>
    private static void Icon(Control c, Rect2 r, string shape, bool ready)
    {
        var col = ready ? Materials.Bone : Materials.Ash;
        var m = r.GetCenter();
        switch (shape)
        {
            case "cone" or "nova" or "blast":
                c.DrawPolyline(new[] { m + new Vector2(-18, 14), m + new Vector2(18, -4), m + new Vector2(18, 20), m + new Vector2(-18, 14) }, Materials.Ember, 2);
                c.DrawLine(m + new Vector2(-10, 12), m + new Vector2(10, 4), col, 2);
                c.DrawLine(m + new Vector2(0, 9), m + new Vector2(12, 14), col, 2);
                break;
            case "dash" or "teleport":
                c.DrawLine(m + new Vector2(-18, 0), m + new Vector2(12, 0), col, 4);
                c.DrawPolyline(new[] { m + new Vector2(4, -10), m + new Vector2(16, 0), m + new Vector2(4, 10) }, col, 4);
                c.DrawLine(m + new Vector2(-18, -8), m + new Vector2(-6, -8), col, 2);
                c.DrawLine(m + new Vector2(-18, 8), m + new Vector2(-6, 8), col, 2);
                break;
            case "buff" or "barrier" or "aura" or "self":
                c.DrawArc(m, 16, 0, Mathf.Tau, 24, col, 3);
                c.DrawLine(m + new Vector2(0, -9), m + new Vector2(0, 9), col, 3);
                c.DrawLine(m + new Vector2(-9, 0), m + new Vector2(9, 0), col, 3);
                break;
            default:
                c.DrawLine(m + new Vector2(-16, 16), m + new Vector2(14, -14), col, 4);
                c.DrawLine(m + new Vector2(6, -18), m + new Vector2(18, -6), col, 4);
                break;
        }
    }

    /// <summary>The boss's bar shows once it has noticed the hero (line of sight within its reach) or has been hurt.</summary>
    private bool BossShown
    {
        get
        {
            var b = _session.Boss; var h = _session.Hero;
            if (!b.Alive) return false;
            if (b.Health < b.MaxHealth || b.Controller is { PendingAbility: not null } || b.Controller is { BasicTimer: > 0 }) return true;
            return Math.Max(Math.Abs(b.X - h.X), Math.Abs(b.Y - h.Y)) <= 12;
        }
    }

    /// <summary>Boss bar (top centre, as UMBRA draws it): name, life, phase, and what it is preparing.</summary>
    private void DrawBoss(Control c, Vector2 size)
    {
        if (!BossShown) return;
        var b = _session.Boss; var ctl = b.Controller;
        // right of the objective panel (which ends near x = 440 at 1280 wide)
        var r = new Rect2(Mathf.Max(470, size.X / 2 - 180), 34, 440, 20);
        UiKit.Text(c, b.Name.ToUpperInvariant() + (ctl is { Phase: 2 } ? "  —  FASE II" : ""), new Vector2(r.Position.X, r.Position.Y - 8), 11, Materials.Gold, HorizontalAlignment.Center, r.Size.X);
        UiKit.Bar(c, r, b.Health, b.MaxHealth, Materials.Blood, $"{b.Health}/{b.MaxHealth}");
        if (ctl is { MessageTimer: > 0 } && ctl.Message != "")
            UiKit.Text(c, ctl.Message, new Vector2(r.Position.X, r.End.Y + 24), 22, Materials.Bone, HorizontalAlignment.Center, r.Size.X);
    }

    private void DrawBanner(Control c, Vector2 size)
    {
        if (ShowFps) UiKit.Text(c, $"{Engine.GetFramesPerSecond()} FPS", new Vector2(16, size.Y - 14), 11, Materials.Ash);
        if (Banner == "") return;
        bool dead = Banner == "dead";
        var r = new Rect2(size.X / 2 - 220, size.Y * .32f, 440, 92);
        UiKit.Frame(c, r, dead ? Materials.Blood : Materials.Gold);
        UiKit.TextIn(c, dead ? "VOCÊ CAIU" : "CRIPTA LIMPA", new Rect2(r.Position + new Vector2(0, 10), new Vector2(r.Size.X, 44)), 33,
                     dead ? Materials.Rgb(230, 90, 80) : Materials.Gold);
        string hint = DeviceTracker.Current == Device.Keyboard ? "Enter: recomeçar" : "A / Cruz: recomeçar";
        UiKit.TextIn(c, hint, new Rect2(r.Position + new Vector2(0, 56), new Vector2(r.Size.X, 28)), 22, Materials.Ash);
    }
}
