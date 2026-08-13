using System.Drawing.Imaging;
using System.Globalization;
using System.Text;

namespace ClayMonstersPet;

internal sealed class PetSheet
{
    private const int FacingCount = 3;
    private const int ActionCount = 3;
    private const int RequiredClipCount = 8;

    private readonly Bitmap[][][] frames = new Bitmap[FacingCount][][];

    public PetSheet()
    {
        for (int f = 0; f < FacingCount; f++)
        {
            frames[f] = new Bitmap[ActionCount][];
            for (int a = 0; a < ActionCount; a++)
            {
                frames[f][a] = Array.Empty<Bitmap>();
            }
        }
    }

    public void SetClip(PetFacing facing, PetAction action, Bitmap[] clip)
    {
        frames[(int)facing][(int)action] = clip ?? Array.Empty<Bitmap>();
    }

    public int GetCount(PetFacing facing, PetAction action) =>
        frames[(int)facing][(int)action].Length;

    public Bitmap? GetFrame(PetFacing facing, PetAction action, int index)
    {
        Bitmap[] clip = frames[(int)facing][(int)action];
        if (clip.Length == 0)
        {
            return null;
        }

        int safe = index % clip.Length;
        if (safe < 0)
        {
            safe += clip.Length;
        }

        return clip[safe];
    }

    public void Dispose()
    {
        HashSet<Bitmap> unique = new();
        for (int f = 0; f < FacingCount; f++)
        {
            for (int a = 0; a < ActionCount; a++)
            {
                foreach (Bitmap bitmap in frames[f][a])
                {
                    if (bitmap != null && unique.Add(bitmap))
                    {
                        bitmap.Dispose();
                    }
                }

                frames[f][a] = Array.Empty<Bitmap>();
            }
        }
    }

    public static PetSheet Load(string cacheDirectory)
    {
        string manifestPath = Path.Combine(cacheDirectory, "manifest.txt");
        if (!File.Exists(manifestPath))
        {
            throw new FileNotFoundException("manifest.txt がありません", manifestPath);
        }

        Dictionary<(int facing, int action), List<string>> clips = new();
        foreach (string raw in File.ReadAllLines(manifestPath, Encoding.UTF8))
        {
            if (string.IsNullOrWhiteSpace(raw) || !raw.StartsWith("frame=", StringComparison.Ordinal))
            {
                continue;
            }

            string value = raw.Substring("frame=".Length);
            string[] parts = value.Split(',');
            if (parts.Length != 4
                || !int.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out int facing)
                || !int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out int action)
                || !int.TryParse(parts[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out int frameIndex))
            {
                continue;
            }

            if (facing < 0 || facing >= FacingCount || action < 0 || action >= ActionCount)
            {
                throw new InvalidDataException($"非対応クリップ facing={facing} action={action}");
            }

            if (!clips.TryGetValue((facing, action), out List<string>? list))
            {
                list = new List<string>();
                clips[(facing, action)] = list;
            }

            while (list.Count <= frameIndex)
            {
                list.Add(string.Empty);
            }

            list[frameIndex] = parts[3];
        }

        if (!HasRequiredClips(clips))
        {
            throw new InvalidDataException("クリップは斜め2方向の3動作と正面の待機移動が必要です");
        }

        PetSheet sheet = new();
        for (int facing = 0; facing < FacingCount; facing++)
        {
            for (int action = 0; action < ActionCount; action++)
            {
                if (!clips.TryGetValue((facing, action), out List<string>? files) || files.Count == 0)
                {
                    if (IsRequiredClip(facing, action))
                    {
                        throw new InvalidDataException($"clip欠損 facing={facing} action={action}");
                    }

                    continue;
                }

                Bitmap[] bitmaps = new Bitmap[files.Count];
                for (int i = 0; i < files.Count; i++)
                {
                    string path = Path.Combine(cacheDirectory, files[i]);
                    using Bitmap loaded = new Bitmap(path);
                    bitmaps[i] = PrepareKeyedFrame(loaded);
                }

                sheet.SetClip((PetFacing)facing, (PetAction)action, bitmaps);
            }
        }

        return sheet;
    }

    private static Bitmap PrepareKeyedFrame(Bitmap source)
    {
        Bitmap keyed = new(source.Width, source.Height, PixelFormat.Format32bppArgb);
        for (int y = 0; y < source.Height; y++)
        {
            for (int x = 0; x < source.Width; x++)
            {
                Color pixel = source.GetPixel(x, y);
                keyed.SetPixel(
                    x,
                    y,
                    pixel.A < 20
                        ? Color.Magenta
                        : Color.FromArgb(255, pixel.R, pixel.G, pixel.B));
            }
        }

        return keyed;
    }

    private static bool HasRequiredClips(Dictionary<(int facing, int action), List<string>> clips)
    {
        int required = 0;
        for (int facing = 0; facing < FacingCount; facing++)
        {
            for (int action = 0; action < ActionCount; action++)
            {
                if (!IsRequiredClip(facing, action))
                {
                    continue;
                }

                if (!clips.TryGetValue((facing, action), out List<string>? files)
                    || files == null
                    || files.Count == 0)
                {
                    return false;
                }

                required++;
            }
        }

        return required == RequiredClipCount;
    }

    private static bool IsRequiredClip(int facing, int action)
    {
        if (facing < 0 || facing >= FacingCount || action < 0 || action >= ActionCount)
        {
            return false;
        }

        if (facing == (int)PetFacing.Front)
        {
            return action != (int)PetAction.Attack;
        }

        return true;
    }
}
