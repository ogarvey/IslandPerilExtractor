using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats; // Needed for Image<Rgba32> and pixel formats
using System;
using System.Collections.Generic;

public static class ImageUtils // Example class name
{
	// Assuming VgaTranslate is defined elsewhere and returns a byte
	// public static byte VgaTranslate(byte value) { /* ... implementation ... */ }

	/// <summary>
	/// Converts a byte array (assumed RGB triplets) to a list of ImageSharp Colors.
	/// </summary>
	/// <param name="bytes">Input byte array (R, G, B, R, G, B...).</param>
	/// <param name="translate">Optional flag to apply VGA translation.</param>
	/// <returns>A list of SixLabors.ImageSharp.Color objects.</returns>
	public static List<Color> ConvertBytesToImageSharpRGB(byte[] bytes, bool translate = false)
	{
		// Use ImageSharp's Color type
		List<Color> colors = new List<Color>();

		for (int i = 0; i < bytes.Length - 2; i += 3)
		{
			// Assuming VgaTranslate exists and returns byte
			byte red = translate ? VgaTranslate(bytes[i]) : bytes[i];
			byte green = translate ? VgaTranslate(bytes[i + 1]) : bytes[i + 1];
			byte blue = translate ? VgaTranslate(bytes[i + 2]) : bytes[i + 2];

			// Create an ImageSharp Color object from RGB values
			Color color = Color.FromRgb(red, green, blue);
			colors.Add(color);
		}
		return colors;
	}

	/// <summary>
	/// Generates an ImageSharp image using a Color Look-Up Table (CLUT).
	/// </summary>
	/// <param name="palette">The color palette (list of ImageSharp Colors).</param>
	/// <param name="clut7Bytes">Byte array where each byte is an index into the palette.</param>
	/// <param name="width">Desired image width.</param>
	/// <param name="height">Desired image height.</param>
	/// <param name="useTransparency">Whether to treat specific indices as transparent.</param>
	/// <param name="transparencyIndex">The index (or threshold) to treat as transparent.</param>
	/// <param name="lowerIndexes">If true, indices <= transparencyIndex are transparent; otherwise >=.</param>
	/// <param name="fixedIndex">If true, only the exact transparencyIndex is transparent (overrides lowerIndexes).</param>
	/// <returns>An ImageSharp Image<Rgba32>.</returns>
	public static Image<Rgba32> GenerateClutImageSharp(
			List<Color> palette,
			byte[] clut7Bytes,
			int width,
			int height,
			bool useTransparency = false,
			int transparencyIndex = 0,
			bool lowerIndexes = true,
			bool fixedIndex = false)
	{
		// Use ImageSharp's generic Image<TPixel>. Rgba32 is common for supporting transparency.
		var clutImage = new Image<Rgba32>(width, height); // Creates an image with a default background (usually black transparent)

		try
		{
			// Access pixels using Span<T> for potentially better performance,
			// but direct indexer access is fine for clarity and matches original logic.
			for (int y = 0; y < height; y++)
			{
				for (int x = 0; x < width; x++)
				{
					var i = y * width + x;
					if (i >= clut7Bytes.Length) continue; // Avoid IndexOutOfRangeException if clutBytes is too short

					var paletteIndex = clut7Bytes[i];
					Color color; // Use ImageSharp Color

					// Determine the base color from the palette
					if (paletteIndex < palette.Count)
					{
						color = palette[paletteIndex];
					}
					else
					{
						// Handle index out of bounds for the palette (wrap around)
						color = palette.Count > 0 ? palette[paletteIndex % palette.Count] : Color.Magenta; // Magenta indicates error/missing palette
					}

					// Apply transparency rules
					bool makeTransparent = false;
					if (useTransparency)
					{
						if (fixedIndex)
						{
							if (paletteIndex == transparencyIndex)
							{
								makeTransparent = true;
							}
							// Add handling for the modulo case if needed for fixedIndex transparency
							// else if (paletteIndex >= palette.Count && (paletteIndex % palette.Count) == transparencyIndex) {
							//     makeTransparent = true;
							// }
						}
						else // Use range-based transparency
						{
							// Check original index against threshold
							if ((lowerIndexes && paletteIndex <= transparencyIndex) || (!lowerIndexes && paletteIndex >= transparencyIndex))
							{
								makeTransparent = true;
							}
						}

						// Specific check from original code: (paletteIndex > 0 && paletteIndex % palette.Count == 0)
						// This condition seems odd. It makes the first color (index 0 after modulo) transparent *if* the original index wasn't 0.
						// Replicating it, but double-check if this logic is exactly what's intended.
						if (!makeTransparent && paletteIndex > 0 && palette.Count > 0 && paletteIndex % palette.Count == 0)
						{
							// This potentially overrides a non-transparent color if its index is a multiple of palette.Count > 0
							// Let's clarify if this is intended or if it should also respect `fixedIndex` etc.
							// For now, replicating the original logic structure:
							if (!fixedIndex || paletteIndex != transparencyIndex) // Avoid making transparent twice if fixedIndex applies
							{
								// This separate check might interact confusingly with the others.
								// Maybe refactor the transparency logic if possible.
								// Based on original code: if it matches *this specific* condition, make transparent.
								// Let's check the *original code again*:
								// Original: if ((paletteIndex > 0 && paletteIndex % palette.Count == 0) || (useTransparency && fixedIndex && paletteIndex == transparencyIndex)) { color = Color.Transparent; }
								// This implies these conditions *override* the previous color assignment.

								// Let's restructure the logic slightly for clarity:

								Color potentialColor = paletteIndex < palette.Count ? palette[paletteIndex] : (palette.Count > 0 ? palette[paletteIndex % palette.Count] : Color.Magenta);
								bool isTransparent = false;

								if (useTransparency)
								{
									if (fixedIndex)
									{
										isTransparent = paletteIndex == transparencyIndex;
									}
									else
									{
										isTransparent = (lowerIndexes && paletteIndex <= transparencyIndex) || (!lowerIndexes && paletteIndex >= transparencyIndex);
									}
								}

								// Apply the specific override from the original code *after* the main transparency check
								if (!isTransparent && paletteIndex > 0 && palette.Count > 0 && paletteIndex % palette.Count == 0)
								{
									//This applies transparency if the index maps to index 0 via modulo, but wasn't originally 0.
									//Let's assume this should also be transparent.
									isTransparent = true;
								}

								color = isTransparent ? Color.Transparent : potentialColor;


							} // End inner transparency check block - this structure seems overly complex now

						} // End outer transparency check block
					} // End useTransparency block

					// *** Simplified Transparency Logic based on re-reading original ***

					Color baseColor;
					if (paletteIndex < palette.Count)
					{
						baseColor = palette[paletteIndex];
					}
					else
					{
						baseColor = palette.Count > 0 ? palette[paletteIndex % palette.Count] : Color.Magenta;
					}

					bool shouldBeTransparent = false;
					if (useTransparency && fixedIndex)
					{
						// Fixed index mode: only the specific index is transparent
						shouldBeTransparent = paletteIndex == transparencyIndex;
					}
					else if (useTransparency && !fixedIndex)
					{
						// Range mode: indices below/above or equal to the threshold are transparent
						shouldBeTransparent = (lowerIndexes && paletteIndex <= transparencyIndex) || (!lowerIndexes && paletteIndex >= transparencyIndex);
					}

					// Check the specific override condition from the original code AFTER regular transparency logic.
					// This condition makes index 0 (via modulo, if original index > 0) transparent,
					// OR if fixedIndex transparency matches.
					if ((paletteIndex > 0 && palette.Count > 0 && paletteIndex % palette.Count == 0) || (useTransparency && fixedIndex && paletteIndex == transparencyIndex))
					{
						shouldBeTransparent = true;
					}


					// Set the pixel using the ImageSharp indexer
					// It implicitly converts ImageSharp Color to Rgba32
					clutImage[x, y] = shouldBeTransparent ? Color.Transparent : baseColor;
				}
			}
		}
		catch (Exception ex) // Catch specific exceptions if possible
		{
			// Log the exception details (recommended)
			Console.WriteLine($"Error generating CLUT image: {ex.Message}");
			// Return the partially generated image, matching original behavior
			return clutImage;
		}

		return clutImage;
	}

	// Dummy VgaTranslate function for completeness - replace with your actual implementation
	public static byte VgaTranslate(byte value)
	{
		// Example: Simple scaling for VGA's 6-bit color to 8-bit
		// Actual VGA palettes might need more complex mapping
		return (byte)(value * 255 / 63);
	}
}
