using Godot;

namespace Umbra.Game.Ui;

/// <summary>A vertical list of rows with one focused row (keyboard/pad steps, mouse hover). Base of the simple menus.</summary>
public abstract partial class ListScreen : Screen
{
    protected int Focus;
    protected abstract (string Label, string? Hint, bool Enabled)[] Rows { get; }
    protected abstract void Activate(int index);
    public override string FocusText => Rows.Length > 0 ? Rows[Math.Clamp(Focus, 0, Rows.Length - 1)].Label : "";

    public override void OnNav(Nav nav)
    {
        var rows = Rows;
        switch (nav)
        {
            case Nav.Up: Focus = Step(rows, Focus, -1); break;
            case Nav.Down: Focus = Step(rows, Focus, 1); break;
            case Nav.Accept: if (rows[Focus].Enabled) Activate(Focus); break;
            case Nav.Back: Back(); break;
        }
    }

    protected virtual void Back() { }

    private static int Step((string, string?, bool Enabled)[] rows, int from, int dir)
    {
        for (int i = 1; i <= rows.Length; i++)
        {
            int next = ((from + dir * i) % rows.Length + rows.Length) % rows.Length;
            if (rows[next].Enabled) return next;
        }
        return from;
    }

    protected void PaintRows(Vector2 origin, float width)
    {
        var rows = Rows; float y = origin.Y;
        for (int i = 0; i < rows.Length; i++)
        {
            float h = rows[i].Hint != null ? 64 : 46;
            var r = new Rect2(origin.X, y, width, h);
            Parts.Row(this, r, rows[i].Label, i == Focus, rows[i].Hint, rows[i].Enabled);
            int index = i;
            Hit(r, () => { if (Rows[index].Enabled) Focus = index; }, () => { if (Rows[index].Enabled) Activate(index); });
            y += h + 10;
        }
    }
}

/// <summary>Main menu (approved mockup): the crypt behind, darker on the left where the menu lives.</summary>
public partial class MainMenuScreen : ListScreen
{
    private readonly Main _main;
    public MainMenuScreen(Main main) { _main = main; }

    protected override (string, string?, bool)[] Rows => new (string, string?, bool)[]
    {
        ("Continuar", _main.RunInProgress ? $"Bárbaro — Cripta Esquecida — {_main.Sim.EnemiesRemaining} inimigos restantes" : "Nenhuma partida em andamento", _main.RunInProgress),
        ("Novo jogo", null, true), ("Configurações", null, true), ("Créditos", null, true), ("Sair", null, true),
    };

    public override void OnOpen() { if (!_main.RunInProgress && Focus == 0) Focus = 1; }

    protected override void Activate(int index)
    {
        switch (index)
        {
            case 0: Stack.Clear(); break;
            case 1: _main.StartRun(_main.Options.Seed); _main.RunInProgress = true; Stack.Clear(); break;
            case 2: Stack.Push(new SettingsScreen(_main)); break;
            case 3: Stack.Push(new CreditsScreen()); break;
            case 4: GetTree().Quit(); break;
        }
    }

    protected override void Paint()
    {
        for (int i = 0; i < 32; i++)
            DrawRect(new Rect2(i * Size.X / 32, 0, Size.X / 32 + 1, Size.Y), new Color(Parts.Dim, Mathf.Lerp(.92f, .25f, i / 31f)));
        UiKit.Text(this, "UMBRA", new Vector2(102, 192), 66, new Color(0, 0, 0, .8f));
        UiKit.Text(this, "UMBRA", new Vector2(96, 186), 66, Materials.Gold);
        UiKit.Text(this, "ASHES BELOW", new Vector2(100, 214), 22, Materials.Ash);
        DrawRect(new Rect2(98, 230, 260, 2), Materials.Iron);
        PaintRows(new Vector2(110, 270), 420);
        UiKit.Text(this, "vertical slice  —  Godot 4.7.2", new Vector2(40, Size.Y - 44), 11, Parts.Faint);
        Parts.Prompts(this, Size.Y - 22, ("Enter", "A", "Confirmar"), ("Setas", "Dir.", "Navegar"));
    }
}

/// <summary>Pause menu: the world stops; Esc/Start or B closes it.</summary>
public partial class PauseScreen : ListScreen
{
    private readonly Main _main;
    public PauseScreen(Main main) { _main = main; }

    protected override (string, string?, bool)[] Rows => new (string, string?, bool)[]
    {
        ("Continuar", null, true), ("Personagem", null, true), ("Habilidades", null, true), ("Configurações", null, true),
        ("Recomeçar a cripta", null, true), ("Menu principal", null, true), ("Sair do jogo", null, true),
    };

    protected override void Activate(int index)
    {
        switch (index)
        {
            case 0: Stack.Pop(); break;
            case 1: Stack.Pop(); Stack.Push(new CharacterScreen(_main)); break;
            case 2: Stack.Pop(); Stack.Push(new TreeScreen(_main)); break;
            case 3: Stack.Push(new SettingsScreen(_main)); break;
            case 4: Stack.Pop(); _main.StartRun(_main.Options.Seed); break;
            case 5: Stack.Pop(); Stack.Push(new MainMenuScreen(_main)); break;
            case 6: GetTree().Quit(); break;
        }
    }

    protected override void Back() => Stack.Pop();

    protected override void Paint()
    {
        Parts.Shade(this, .6f);
        var r = new Rect2(Size / 2 - new Vector2(230, 270), new Vector2(460, 540));
        UiKit.Frame(this, r);
        UiKit.TextIn(this, "PAUSA", new Rect2(r.Position + new Vector2(0, 16), new Vector2(r.Size.X, 44)), 33, Materials.Gold);
        PaintRows(r.Position + new Vector2(50, 76), 360);
        Parts.Prompts(this, Size.Y - 22, ("Enter", "A", "Confirmar"), ("Esc", "B", "Voltar"));
    }
}

public partial class CreditsScreen : Screen
{
    public override void OnNav(Nav nav) { if (nav is Nav.Back or Nav.Accept) Stack.Pop(); }

    protected override void Paint()
    {
        Parts.Shade(this, .92f);
        var r = new Rect2(Size / 2 - new Vector2(320, 200), new Vector2(640, 400));
        Parts.Panel(this, r, "CRÉDITOS");
        float y = r.Position.Y + 90;
        foreach (var (role, name) in new[] { ("Direção criativa", "Diretor criativo do UMBRA"), ("Engenharia e arte procedural", "Claude Code"),
                                             ("Motor", "Godot Engine 4.7.2 (.NET)"), ("Fonte", "UMBRA Pixel, desenhada para o UMBRA") })
        {
            UiKit.Text(this, role.ToUpperInvariant(), new Vector2(r.Position.X + 40, y), 11, Materials.Ash);
            UiKit.Text(this, name, new Vector2(r.Position.X + 40, y + 30), 22, Materials.Bone);
            y += 66;
        }
        Hit(new Rect2(Vector2.Zero, Size), null, () => Stack.Pop());
        Parts.Prompts(this, Size.Y - 22, ("Esc", "B", "Voltar"));
    }
}
