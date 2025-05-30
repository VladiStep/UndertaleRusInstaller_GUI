using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;

namespace UndertaleRusInstallerGUI;

public class TextureInfo
{
    public string Source;
    public int Width;
    public int Height;
}

public enum SplitType
{
    Horizontal,
    Vertical,
}

public enum BestFitHeuristic
{
    Area,
    MaxOneAxis,
}

public class Node
{
    public Rectangle Bounds;
    public TextureInfo Texture;
    public SplitType SplitType;
}

public class Atlas
{
    public int Width;
    public int Height;
    public List<Node> Nodes;
}

public class Packer
{
    public List<TextureInfo> SourceTextures;
    public StringWriter Log;
    public StringWriter Error;
    public int Padding;
    public int AtlasSize;
    public bool DebugMode;
    public BestFitHeuristic FitHeuristic;
    public List<Atlas> Atlasses;

    public Packer()
    {
        SourceTextures = new List<TextureInfo>();
        Log = new StringWriter();
        Error = new StringWriter();
    }

    public void Process(string _sourceDir, string _pattern, int _atlasSize, int _padding, bool _debugMode, Action _incrValDeleg, Action<double> _setMaxDeleg)
    {
        Padding = _padding;
        AtlasSize = _atlasSize;
        DebugMode = _debugMode;
        //1: scan for all the textures we need to pack
        ScanForTextures(_sourceDir, _pattern, _incrValDeleg, _setMaxDeleg);
        List<TextureInfo> textures = SourceTextures.ToList();
        //2: generate as many atlasses as needed (with the latest one as small as possible)
        Atlasses = new List<Atlas>();
        while (textures.Count > 0)
        {
            Atlas atlas = new Atlas();
            atlas.Width = _atlasSize;
            atlas.Height = _atlasSize;
            List<TextureInfo> leftovers = LayoutAtlas(textures, atlas);
            if (leftovers.Count == 0)
            {
                // we reached the last atlas. Check if this last atlas could have been twice smaller
                while (leftovers.Count == 0)
                {
                    atlas.Width /= 2;
                    atlas.Height /= 2;
                    leftovers = LayoutAtlas(textures, atlas);
                }
                // we need to go 1 step larger as we found the first size that is to small
                atlas.Width *= 2;
                atlas.Height *= 2;
                leftovers = LayoutAtlas(textures, atlas);
            }
            Atlasses.Add(atlas);
            textures = leftovers;
        }
    }

    public void SaveAtlasses(string _destination, Action _incrValDeleg)
    {
        int atlasCount = 0;
        string prefix = _destination.Replace(Path.GetExtension(_destination), "");
        StreamWriter tw = new StreamWriter(_destination);
        tw.WriteLine("source_tex, atlas_tex, x, y, width, height");
        foreach (Atlas atlas in Atlasses)
        {
            string atlasName = String.Format(prefix + "{0:000}" + ".png", atlasCount);
            //1: Save images
            Image img = CreateAtlasImage(atlas, _incrValDeleg);
            //DPI fix start
            Bitmap ResolutionFix = new Bitmap(img);
            ResolutionFix.SetResolution(96.0F, 96.0F);
            Image img2 = ResolutionFix;
            //DPI fix end
            img2.Save(atlasName, System.Drawing.Imaging.ImageFormat.Png);
            //2: save description in file
            foreach (Node n in atlas.Nodes)
            {
                if (n.Texture != null)
                {
                    tw.Write(n.Texture.Source + ", ");
                    tw.Write(atlasName + ", ");
                    tw.Write((n.Bounds.X).ToString() + ", ");
                    tw.Write((n.Bounds.Y).ToString() + ", ");
                    tw.Write((n.Bounds.Width).ToString() + ", ");
                    tw.WriteLine((n.Bounds.Height).ToString());
                }
            }
            ++atlasCount;
            img2.Dispose();
        }
        tw.Close();
        tw = new StreamWriter(prefix + ".log");
        tw.WriteLine("--- LOG -------------------------------------------");
        tw.WriteLine(Log.ToString());
        tw.WriteLine("--- ERROR -----------------------------------------");
        tw.WriteLine(Error.ToString());
        tw.Close();
    }

    /// <summary>
    /// Reads PNG image size by only reading necessary bytes in the header
    /// </summary>
    public static (int Width, int Height) ReadPNGSize(string filePath)
    {
        static uint ReadUInt32BigEndian(BinaryReader br)
        {
            return (uint)br.ReadByte() << 24 | (uint)br.ReadByte() << 16 | (uint)br.ReadByte() << 8 | (uint)br.ReadByte();
        }

        using FileStream fs = new(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
        using BinaryReader br = new(fs);

        // Check PNG signature (first 8 bytes)
        ulong signature = br.ReadUInt64();
        if (signature != 0x0A1A0A0D474E5089) // [ 137, 80, 78, 71, 13, 10, 26, 10 ]
            throw new InvalidDataException("Not a valid PNG file.");

        // Next chunk should be IHDR
        uint length = ReadUInt32BigEndian(br);
        uint chunkType = br.ReadUInt32();
        if (chunkType != 0x52444849) // 0x52444849 -> "IHDR"
            throw new InvalidDataException("IHDR chunk not found where expected.");

        // Read width and height from IHDR chunk
        int width = (int)ReadUInt32BigEndian(br);
        int height = (int)ReadUInt32BigEndian(br);

        return (width, height);
    }

    private void ScanForTextures(string _path, string _wildcard, Action _incrValDeleg, Action<double> _setMaxDeleg)
    {
        DirectoryInfo di = new(_path);
        FileInfo[] files = di.GetFiles(_wildcard, SearchOption.AllDirectories);

        _setMaxDeleg(files.Length);

        foreach (FileInfo fi in files.OrderBy(x => x.Name))
        {
            try
            {
                _incrValDeleg();

                (int width, int height) = ReadPNGSize(fi.FullName);
                if (width <= AtlasSize && height <= AtlasSize)
                {
                    TextureInfo ti = new()
                    {
                        Source = fi.FullName,
                        Width = width,
                        Height = height
                    };

                    SourceTextures.Add(ti);

                    Log.WriteLine("Added " + fi.FullName);
                }
                else
                {
                    Error.WriteLine(fi.FullName + " is too large to fix in the atlas. Skipping!");
                }
            }
            catch (Exception ex)
            {
                throw new Exception('.' + fi.FullName, ex);
            }
        }
    }

    private void HorizontalSplit(Node _toSplit, int _width, int _height, List<Node> _list)
    {
        Node n1 = new Node();
        n1.Bounds.X = _toSplit.Bounds.X + _width + Padding;
        n1.Bounds.Y = _toSplit.Bounds.Y;
        n1.Bounds.Width = _toSplit.Bounds.Width - _width - Padding;
        n1.Bounds.Height = _height;
        n1.SplitType = SplitType.Vertical;
        Node n2 = new Node();
        n2.Bounds.X = _toSplit.Bounds.X;
        n2.Bounds.Y = _toSplit.Bounds.Y + _height + Padding;
        n2.Bounds.Width = _toSplit.Bounds.Width;
        n2.Bounds.Height = _toSplit.Bounds.Height - _height - Padding;
        n2.SplitType = SplitType.Horizontal;
        if (n1.Bounds.Width > 0 && n1.Bounds.Height > 0)
            _list.Add(n1);
        if (n2.Bounds.Width > 0 && n2.Bounds.Height > 0)
            _list.Add(n2);
    }

    private void VerticalSplit(Node _toSplit, int _width, int _height, List<Node> _list)
    {
        Node n1 = new Node();
        n1.Bounds.X = _toSplit.Bounds.X + _width + Padding;
        n1.Bounds.Y = _toSplit.Bounds.Y;
        n1.Bounds.Width = _toSplit.Bounds.Width - _width - Padding;
        n1.Bounds.Height = _toSplit.Bounds.Height;
        n1.SplitType = SplitType.Vertical;
        Node n2 = new Node();
        n2.Bounds.X = _toSplit.Bounds.X;
        n2.Bounds.Y = _toSplit.Bounds.Y + _height + Padding;
        n2.Bounds.Width = _width;
        n2.Bounds.Height = _toSplit.Bounds.Height - _height - Padding;
        n2.SplitType = SplitType.Horizontal;
        if (n1.Bounds.Width > 0 && n1.Bounds.Height > 0)
            _list.Add(n1);
        if (n2.Bounds.Width > 0 && n2.Bounds.Height > 0)
            _list.Add(n2);
    }

    private TextureInfo FindBestFitForNode(Node _node, List<TextureInfo> _textures)
    {
        TextureInfo bestFit = null;
        float nodeArea = _node.Bounds.Width * _node.Bounds.Height;
        float maxCriteria = 0.0f;
        foreach (TextureInfo ti in _textures)
        {
            switch (FitHeuristic)
            {
            // Max of Width and Height ratios
            case BestFitHeuristic.MaxOneAxis:
                if (ti.Width <= _node.Bounds.Width && ti.Height <= _node.Bounds.Height)
                {
                    float wRatio = (float)ti.Width / (float)_node.Bounds.Width;
                    float hRatio = (float)ti.Height / (float)_node.Bounds.Height;
                    float ratio = wRatio > hRatio ? wRatio : hRatio;
                    if (ratio > maxCriteria)
                    {
                        maxCriteria = ratio;
                        bestFit = ti;
                    }
                }
                break;
            // Maximize Area coverage
            case BestFitHeuristic.Area:
                if (ti.Width <= _node.Bounds.Width && ti.Height <= _node.Bounds.Height)
                {
                    float textureArea = ti.Width * ti.Height;
                    float coverage = textureArea / nodeArea;
                    if (coverage > maxCriteria)
                    {
                        maxCriteria = coverage;
                        bestFit = ti;
                    }
                }
                break;
            }
        }
        return bestFit;
    }

    private List<TextureInfo> LayoutAtlas(List<TextureInfo> _textures, Atlas _atlas)
    {
        List<Node> freeList = new List<Node>();
        List<TextureInfo> textures = new List<TextureInfo>();
        _atlas.Nodes = new List<Node>();
        textures = _textures.ToList();
        Node root = new Node();
        root.Bounds.Size = new Size(_atlas.Width, _atlas.Height);
        root.SplitType = SplitType.Horizontal;
        freeList.Add(root);
        while (freeList.Count > 0 && textures.Count > 0)
        {
            Node node = freeList[0];
            freeList.RemoveAt(0);
            TextureInfo bestFit = FindBestFitForNode(node, textures);
            if (bestFit != null)
            {
                if (node.SplitType == SplitType.Horizontal)
                {
                    HorizontalSplit(node, bestFit.Width, bestFit.Height, freeList);
                }
                else
                {
                    VerticalSplit(node, bestFit.Width, bestFit.Height, freeList);
                }
                node.Texture = bestFit;
                node.Bounds.Width = bestFit.Width;
                node.Bounds.Height = bestFit.Height;
                textures.Remove(bestFit);
            }
            _atlas.Nodes.Add(node);
        }
        return textures;
    }

    private Image CreateAtlasImage(Atlas _atlas, Action _incrValDeleg)
    {
        Image img = new Bitmap(_atlas.Width, _atlas.Height, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        Graphics g = Graphics.FromImage(img);
        if (DebugMode)
        {
            g.FillRectangle(Brushes.Green, new Rectangle(0, 0, _atlas.Width, _atlas.Height));
        }
        foreach (Node n in _atlas.Nodes)
        {
            if (n.Texture != null)
            {
                _incrValDeleg();

                Image sourceImg = Image.FromFile(n.Texture.Source);
                g.DrawImage(sourceImg, n.Bounds);
                if (DebugMode)
                {
                    string label = Path.GetFileNameWithoutExtension(n.Texture.Source);
                    SizeF labelBox = g.MeasureString(label, SystemFonts.MenuFont, new SizeF(n.Bounds.Size));
                    RectangleF rectBounds = new Rectangle(n.Bounds.Location, new Size((int)labelBox.Width, (int)labelBox.Height));
                    g.FillRectangle(Brushes.Black, rectBounds);
                    g.DrawString(label, SystemFonts.MenuFont, Brushes.White, rectBounds);
                }
            }
            else
            {
                g.FillRectangle(Brushes.DarkMagenta, n.Bounds);
                if (DebugMode)
                {
                    string label = n.Bounds.Width.ToString() + "x" + n.Bounds.Height.ToString();
                    SizeF labelBox = g.MeasureString(label, SystemFonts.MenuFont, new SizeF(n.Bounds.Size));
                    RectangleF rectBounds = new Rectangle(n.Bounds.Location, new Size((int)labelBox.Width, (int)labelBox.Height));
                    g.FillRectangle(Brushes.Black, rectBounds);
                    g.DrawString(label, SystemFonts.MenuFont, Brushes.White, rectBounds);
                }
            }
        }
        return img;
    }
}