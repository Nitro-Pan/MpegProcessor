using JpegProcessingConsole;

// See https://aka.ms/new-console-template for more information
//int[,] image = {
//    { 1, 2, 3, 5, 8, 13, 21, 34 },
//    { 1, 2, 3, 5, 8, 13, 21, 34 },
//    { 1, 2, 3, 5, 8, 13, 21, 34 },
//    { 1, 2, 3, 5, 8, 13, 21, 34 },
//    { 1, 2, 3, 5, 8, 13, 21, 34 },
//    { 1, 2, 3, 5, 8, 13, 21, 34 },
//    { 1, 2, 3, 5, 8, 13, 21, 34 },
//    { 1, 2, 3, 5, 8, 13, 21, 34 },
//};

int[,] image = {
    { 1, 1, 1, 1, 1, 1, 1, 1 },
    { 2, 2, 2, 2, 2, 2, 2, 2 },
    { 3, 3, 3, 3, 3, 3, 3, 3 },
    { 5, 5, 5, 5, 5, 5, 5, 5 },
    { 8, 8, 8, 8, 8, 8, 8, 8 },
    { 13, 13, 13, 13, 13, 13, 13, 13 },
    { 21, 21, 21, 21, 21, 21, 21, 21 },
    { 34, 34, 34, 34, 34, 34, 34, 34 },
};

float[,] result = new float[image.GetLength(0), image.GetLength(1)];
for (int u = 0; u < image.GetLength(0); u++) {
    for (int v = 0; v < image.GetLength(1); v++) {
        result[u, v] = DCT.Forwards(u, v, image, 8, 8);
    }
}

int[,] notReallyAnImage = new int[8, 8];
for (int x = 0; x < result.GetLength(0); x++) {
    for (int y = 0; y < result.GetLength(0); y++) { 
         notReallyAnImage[x, y] = (int) MathF.Round(result[x, y]);
    }
}

int[,] originalMaybe = new int[8, 8];
for (int x = 0; x < originalMaybe.GetLength(0); x++) {
    for (int y = 0; y < originalMaybe.GetLength(0); y++) {
        originalMaybe[x, y] = (int) Math.Round(DCT.Backwards(x, y, notReallyAnImage, 8, 8));
    }
}

for (int x = 0; x < originalMaybe.GetLength(0); x++)
{
    for (int y = 0; y < originalMaybe.GetLength(0); y++)
    {
        Console.Write($"{notReallyAnImage[x, y]}, ");
    }
    Console.Write("\n");
}


//for (int x = 0; x < 256; x++) {
//    Console.WriteLine(new YCbCrPixel(new RGBAPixel((byte)x, (byte)x, (byte)x, 255)));
//}
