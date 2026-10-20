The mascot of the website and the mobile app: Jumbo the elephant cub (it replaced the tiger).
Program.cs cuts the blue background out of src.webp and writes, into ./out: the website's jumbo.webp / jumbo-head.webp and its
icons (favicon.png/svg, apple-touch-icon.png, jumbo-icon-512.png) and, into ./out/mobile, the app's jumbo_full.png / jumbo_head.png,
appicon.png, appiconfg.png and splash.png. Run: dotnet run (inside this folder; needs the SkiaSharp 2.88.8 NuGet package).
The old tools/gen-mobile-assets.mjs drew the tiger: do not run its tiger / icon part any more.
