QR codes of jumbo.uz -> ../QR. Run inside this folder:
  dotnet run -- ../../QR                      the site, book 0 and the 10 books (a book opens the list of all its topics: /topics/N)
  dotnet run -- --boxes ../../QR/Boxes        every box of cards (60 words = 4 topics) of every book (/topics/N/K), a folder per book
  dotnet run -- --subjects ../../QR/Subjects  every topic of every book (the words with the sound: /topic/N/M)
(needs the QRCoder 1.8.0, ZXing.Net 0.16.11 and SkiaSharp 2.88.8 NuGet packages; it reads every code back and checks the address)
