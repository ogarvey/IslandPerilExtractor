using System.Text;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.Processing;

var ipwFile = args[0];
var resourceOutputDir = Path.Combine(Path.GetDirectoryName(ipwFile)!, "extracted_resources");
if (string.IsNullOrEmpty(ipwFile) || !File.Exists(ipwFile))
{
  Console.WriteLine("Usage: IPWExtractor <ipw file> [output directory]");
  return;
}
Directory.CreateDirectory(resourceOutputDir);
Console.WriteLine($"Extracting {ipwFile} to {resourceOutputDir}...");
Console.WriteLine();

Extract(ipwFile, resourceOutputDir);
ParseImages(resourceOutputDir);

static void Extract(string ipwFile, string resourceOutputDir)
{
  var ipwData = File.ReadAllBytes(ipwFile);
  for (int i = 0xc; i < ipwData.Length; i++)
  {
    ipwData[i] = (byte)(ipwData[i] ^ 0xAC);
  }


  using var xorReader = new BinaryReader(new MemoryStream(ipwData));
  xorReader.ReadBytes(0x4); // skip 4 bytes
  var fileCount = xorReader.ReadUInt32(); // 0x4
  var fileTableOffset = xorReader.ReadUInt32(); // 0xC

  var namesOffsetsAndLengths = new List<(string name, uint offset, uint length)>(); // offset, length, name

  xorReader.BaseStream.Position = fileTableOffset;
  // each entry is 24 bytes long, 16 bytes null padded name, 4 bytes offset, 4 bytes length
  for (var i = 0; i < fileCount; i++)
  {
    var name = Encoding.UTF8.GetString(xorReader.ReadBytes(16)).TrimEnd('\0');
    var offset = xorReader.ReadUInt32();
    var length = xorReader.ReadUInt32();
    namesOffsetsAndLengths.Add((name, offset, length));
  }

  foreach (var (name, offset, length) in namesOffsetsAndLengths)
  {
    Console.WriteLine($"Writing: {name}, Offset: {offset}, Length: {length}");
    // read the file data
    xorReader.BaseStream.Position = offset;
    var fileData = xorReader.ReadBytes((int)length);
    // write the file data to a file
    var outputFile = Path.Combine(resourceOutputDir, name);
    File.WriteAllBytes(outputFile, fileData);
  }
  Console.WriteLine();
  Console.WriteLine($"Extracted {fileCount} files to {resourceOutputDir}.");
  Console.WriteLine($"Output files saved to {resourceOutputDir}.");
  Console.WriteLine();
}

static void ParseImages(string picDir)
{
  var palFile = Path.Combine(picDir, "PALETTE.PAL");
  var paletteData = File.ReadAllBytes(palFile).Skip(0x8).Take(0x300).ToArray();
  var palette = ImageUtils.ConvertBytesToImageSharpRGB(paletteData, true); // convert the palette data to a palette
  var picOutputDir = Path.Combine(picDir, "pic_output");
  Directory.CreateDirectory(picOutputDir); // create the output directory if it doesn't exist
  var transparentDir = Path.Combine(picDir, "transparent_output");
  Directory.CreateDirectory(transparentDir); // create the transparent output directory if it doesn't exist

  var picFiles = Directory.GetFiles(picDir, "*.PIC");
  Parallel.ForEach(picFiles, picFile =>
  {
    Console.WriteLine($"Processing {picFile}...");
    using var picReader = new BinaryReader(File.OpenRead(picFile));
    picReader.ReadBytes(4); // skip 4 bytes
    var width = picReader.ReadUInt16(); // width of the Image
    var height = picReader.ReadUInt16(); // height of the Image
    var unk1 = picReader.ReadUInt16(); // unknown
    var unk2 = picReader.ReadUInt16(); // unknown
    var unk3 = picReader.ReadUInt16(); // unknown
    var picData = picReader.ReadBytes(width * height); // read the image data
    var image = ImageUtils.GenerateClutImageSharp(palette, picData, width, height); // generate the image from the sprite data
    var pngEncoder = new PngEncoder() { ColorType = PngColorType.Palette }; // create a png encoder with palette color type
                                   // rotaste the image 90 degrees clockwise, and then flip it horizontally
    image.Mutate(x => x.Rotate(90).Flip(FlipMode.Horizontal)); // rotate the image 90 degrees clockwise and flip it horizontally
    var outputFile = Path.Combine(picOutputDir, $"{Path.GetFileNameWithoutExtension(picFile)}_{unk2}_{unk1}_{unk3}.png"); // output file name
    // save the image to a file
    image.SaveAsPng(outputFile, pngEncoder); // save the image as a png file
    // now save the image with transparency
    var transparentImage = ImageUtils.GenerateClutImageSharp(palette, picData, width, height, true); // generate the image from the sprite data with transparency
    outputFile = Path.Combine(transparentDir, $"{Path.GetFileNameWithoutExtension(picFile)}_{unk2}_{unk1}_{unk3}_transparent.png"); // output file name
    transparentImage.Mutate(x => x.Rotate(90).Flip(FlipMode.Horizontal)); // rotate the image 90 degrees clockwise and flip it horizontally
    transparentImage.SaveAsPng(outputFile, pngEncoder); // save the image as a png file
  });
  Console.WriteLine();
  Console.WriteLine($"Processed {picFiles.Length} files.");
  Console.WriteLine($"Output files saved to {picOutputDir}.");
}

