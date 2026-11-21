namespace Jumbo.Shared;

// The names of the topics (the 20 topics of each of the 10 books) in Turkish: the books keep one English name per topic, so a Turkish
// learner would otherwise read "Hello and Goodbye" everywhere. Taken from the topic titles of the Turkish book files
// (SeedData/Books/Turk/book_NN_tr.json); the order is the topic's SequenceNumber, the same as the English book's.
public static class TopicNames
{
    private const int TurkishLanguageId = 4;

    private static readonly string[][] Turkish =
    [
        ["Merhaba ve Hoşça Kal", "Zamirler", "Aile", "İnsanlar", "Vücut", "Evcil Hayvanlar", "Renkler", "Sayılar", "Kıyafetler", "Meyveler", "Günler / Takvim", "Dış Görünüş", "Yiyecekler", "İçecekler", "Yatak Odası", "Banyo", "Aylar", "Sınıf", "Sayılar 2", "Sporlar"],
        ["Soru Kelimeleri", "Hava Durumu", "Zıt Sıfatlar", "Televizyon", "Temel Fiiller", "Meslekler", "Mutfak", "Temel Fiiller 2", "Hobiler", "Sebzeler", "Gitmek", "Şehir", "Temel Fiiller 3", "Sağlık / Hastalık", "Yemek Pişirme", "Duygular", "Restoran", "Ülkeler / Milliyetler", "Vahşi Hayvanlar", "Otel"],
        ["Alışveriş", "Oturma Odası", "Kişilik", "Zıt Sıfatlar 2", "Kıyafetler ve Aksesuarlar", "Ulaşım", "Okul", "Eğitim", "Böcekler", "Ev ve Çevresi", "Müzik", "Ev", "Zaman", "Zıt Sıfatlar 3", "Doğa", "Yönler", "Çiftlik ve Köy", "Para", "Deniz Hayvanları", "Sinema ve Tiyatro"],
        ["Bilgisayar", "Üniversite", "Telefon", "Suç", "Yapmak / Etmek", "Konuşma", "Araba Kullanma", "Bağlaçlar", "Zaman Kelimeleri", "Edatlar ve Hâl Ekleri", "Havalimanı", "Nasıl? (Durum Zarfları)", "Matematik ve Şekiller", "Ev İşleri", "Miktarlar", "Dil Öğrenme", "Ölçüler", "Fiil + Hâl Eki", "Gelmek", "Uyarılar"],
        ["Coğrafya", "Uzay", "Çevre", "Kuşlar", "Balıkçılık ve Tekneler", "Vücut 2", "İlk Yardım ve Eczane", "Aile 2", "Kişilik 2", "Fizik", "Malzemeler ve Aletler", "Dersler", "Banka ve Para", "Yiyecekler 2", "Yolda", "Bayramlar ve Gelenekler", "Geçmiş Zaman Kelimeleri", "Gelecek Planları", "Geçmiş Zaman Fiilleri 1", "Geçmiş Zaman Fiilleri 2"],
        ["İş ve Ofis", "İnternet ve Sosyal Medya", "Haberler ve Medya", "Kitaplar ve Okuma", "Sanat ve Resim", "Kamp ve Doğa", "Bahçe ve Bitkiler", "Doğal Afetler", "Evcil Hayvanlar", "Yapmak, Etmek, Almak", "Kaplar ve Porsiyonlar", "Sayılamayan İsimler", "Kipler", "Tavsiyeler ve Kurallar", "Deneyimler ve Başarılar", "Nesneyi Anlatan Sıfatlar", "Duygu Sıfatları", "Güçlü Sıfatlar", "Temel Fiiller 3", "Temel Fiiller 4"],
        ["Günlük Rutin", "Çocukluk", "İlişkiler", "Hayattaki Olaylar", "Sağlık ve Spor", "Doktorda", "Market Alışverişi", "Beslenme Alışkanlıkları", "Evde Tamirat", "Ev Aletleri", "Değişiklikler", "Seyahat Deneyimleri", "Hava Durumu 2", "Duygular 2", "Alışkanlıklar", "-dır ve -den beri", "Miktar Sözleri", "Ödeme ve Fiyatlar", "Mahalle ve Komşuluk", "«Get» Anlamındaki Fiiller"],
        ["Sevdiklerimiz ve Sevmediklerimiz", "Fiil + -meyi / -mekten", "Fiil + -meye", "Boş Zaman", "Takım Sporları", "Kariyer", "Ders Çalışma Becerileri", "İletişim", "Davetler", "Teknoloji", "Moda", "Kişisel Bakım", "Toplu Taşıma", "Şehir Hizmetleri", "El İşleri", "Sorunlar ve Çözümler", "Sıfat + Hâl Eki", "Amaçlar ve Sebepler", "Bakmak ve Koymak", "Karakter 3"],
        ["Evde Güvenlik", "Acil Durumlar", "İcatlar", "Nasıl Yapılır?", "Enerji Tasarrufu", "Belirtiler ve Tedavi", "Hayaller ve Dilekler", "Kararlar", "Tatil Planı", "Şikâyetler ve İadeler", "Ev Kiralama", "İş Arama", "Sınavlar ve Sonuçlar", "Yarışmalar", "Trafik Kuralları", "Sağlıklı Beslenme", "Kurallar ve Toplum", "Birleşik Fiiller", "Bağlayıcı Sözler", "Mutfak Fiilleri"],
        ["Aktarma Fiilleri", "Hatalar ve Pişmanlıklar", "Hayat Hikâyeleri", "Suç ve Adalet", "Kazalar ve Yaralanmalar", "Şans ve Tesadüf", "Arkadaşlık", "Aşk ve Evlilik", "İş Dünyası ve Toplantılar", "Telefon Görüşmeleri", "Zihin ve Psikoloji", "Medya ve Röportajlar", "Yolculuk Sorunları", "Tepkiler", "Konuşma Fiilleri", "Görüşler ve Tartışmalar", "Önemli Kavramlar", "Deyimler", "Sorular ve Ricalar", "Anlatımda Zaman Sözleri"],
    ];

    public static string Localize(string languageCode, int bookNumber, int sequenceNumber, string english) =>
        Localize(languageCode == "tr" ? TurkishLanguageId : 0, bookNumber, sequenceNumber, english);

    // The name to show for a topic of the given book/sequence number in the given language (the English name when there is no translation).
    public static string Localize(int languageId, int bookNumber, int sequenceNumber, string english)
    {
        if (languageId == TurkishLanguageId
            && bookNumber >= 1 && bookNumber <= Turkish.Length
            && sequenceNumber >= 1 && sequenceNumber <= Turkish[bookNumber - 1].Length)
            return Turkish[bookNumber - 1][sequenceNumber - 1];
        return english;
    }
}
