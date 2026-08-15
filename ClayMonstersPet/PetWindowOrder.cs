using System.Runtime.InteropServices;

namespace ClayMonstersPet;

/// <summary>
/// ペット窓の重ね順設定
/// </summary>
internal static class PetWindowOrder
{
    private static readonly IntPtr HwndBottom = new(1);
    private const uint SwpNosize = 0x0001;
    private const uint SwpNomove = 0x0002;
    private const uint SwpNoactivate = 0x0010;

    /// <summary>
    /// 最前面ならtrue最背面ならfalse
    /// </summary>
    public static bool StayOnTop { get; set; } = true;

    /// <summary>
    /// Formへ重ね順を適用する
    /// </summary>
    public static void Apply(Form form)
    {
        if (form == null || form.IsDisposed)
        {
            return;
        }

        form.TopMost = StayOnTop;
        if (form.IsHandleCreated)
        {
            ApplyZOrder(form);
            return;
        }

        form.HandleCreated += (_, _) =>
        {
            if (!form.IsDisposed)
            {
                ApplyZOrder(form);
            }
        };
    }

    private static void ApplyZOrder(Form form)
    {
        form.TopMost = StayOnTop;
        if (StayOnTop)
        {
            return;
        }

        SetWindowPos(
            form.Handle,
            HwndBottom,
            0,
            0,
            0,
            0,
            SwpNosize | SwpNomove | SwpNoactivate);
    }

    [DllImport("user32.dll")]
    private static extern bool SetWindowPos(
        IntPtr hWnd,
        IntPtr hWndInsertAfter,
        int x,
        int y,
        int cx,
        int cy,
        uint uFlags);
}
