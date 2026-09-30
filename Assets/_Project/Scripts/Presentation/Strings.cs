using System;
using OneMoreMove.Core;
using OneMoreMove.Session;

namespace OneMoreMove.Presentation
{
    /// <summary>Player-facing Turkish text. Kept in one place so a localisation package can replace it later.</summary>
    public static class Strings
    {
        public const string GameTitle = "Bir Hamle Daha";
        public const string Continue = "Devam Et";
        public const string Levels = "Bölümler";
        public const string Settings = "Ayarlar";
        public const string Quit = "Çıkış";
        public const string Back = "Geri";
        public const string Close = "Kapat";
        public const string Menu = "Menü";
        public const string Undo = "Geri Al";
        public const string Restart = "Yeniden Başlat";
        public const string HintLabel = "İpucu";
        public const string NextLevel = "Sonraki Bölüm";
        public const string Replay = "Tekrar Oyna";
        public const string LevelComplete = "Bölüm Tamamlandı";
        public const string AllLevelsComplete = "Tüm bölümler tamamlandı!";
        public const string Locked = "Kilitli";
        public const string RestartConfirm = "Bölüm baştan başlasın mı? Bu denemedeki hamleler silinir.";
        public const string Yes = "Evet";
        public const string No = "Vazgeç";
        public const string HintThinking = "İpucu hazırlanıyor…";
        public const string ReducedMotion = "Animasyonları azalt";
        public const string MovePreview = "Hamle önizlemesi (yön düğmesinin üzerine gel)";
        public const string TextSize = "Yazı boyutu";

        public static string Moves(int count) => $"Hamle: {count}";
        public static string UndoAvailable(int count) => $"Geri alınabilir: {count}";
        public static string BestMoves(int? best) => best.HasValue ? $"En iyi: {best.Value}" : "En iyi: -";
        public static string LevelNumber(int index) => (index + 1).ToString();
        public static string MovesResult(int moves, int par) => $"Hamle: {moves}  (hedef {par})";

        public static string Reject(RejectReason reason)
        {
            switch (reason)
            {
                case RejectReason.OutOfBounds: return "Tahtanın dışına çıkılamaz";
                case RejectReason.Wall: return "Duvar var";
                case RejectReason.GateClosed: return "Kapı kapalı";
                case RejectReason.BlockedByEcho: return "Yankı yolu kapatıyor";
                case RejectReason.LevelAlreadyWon: return "Bölüm zaten tamamlandı";
                default: return "Bu hamle yapılamaz";
            }
        }

        public static string EchoBlocked(EchoBlockReason reason)
        {
            switch (reason)
            {
                case EchoBlockReason.GateClosed: return "Yankının önünde kapalı kapı var, yerinde kaldı";
                case EchoBlockReason.BlockedByPlayer: return "Yankı sana çarptı, yerinde kaldı";
                default: return "Yankının yolu kapalı, yerinde kaldı";
            }
        }

        public static string HintText(Hint hint)
        {
            switch (hint.Kind)
            {
                case HintKind.MechanicReminder: return Reminder(hint.Topic);
                case HintKind.SuggestedDirection:
                    return hint.SuggestedDirection == Direction.Wait
                        ? $"Önerilen: bir tur bekle (hedefe en az {hint.MovesToGoal} hamle)"
                        : $"Önerilen yön: {DirectionName(hint.SuggestedDirection ?? Direction.Up)} (hedefe en az {hint.MovesToGoal} hamle)";
                case HintKind.SuggestUndo: return "Bu durumdan hedefe ulaşılamıyor. Geri almayı ya da yeniden başlatmayı dene.";
                case HintKind.AlreadySolved: return "Bölüm tamamlandı.";
                default: return "İpucu hazırlanamadı.";
            }
        }

        public static string Reminder(MechanicTopic topic)
        {
            switch (topic)
            {
                case MechanicTopic.Gates:
                    return "Hatırlatma: Her başarılı hamlede bütün kapılar açılır ya da kapanır. Kapalı kapıya girilemez; üstündeki taş ise çıkabilir. Beklemek de bir hamledir ve kapıları çevirir.";
                case MechanicTopic.Echo:
                    return "Hatırlatma: Yankı her hamlede senin yönünün tersine gider. Önü kapalıysa yerinde kalır; onun bulunduğu kareye giremezsin.";
                default:
                    return "Hatırlatma: Taşın dört yönden birine bir kare gider ya da bir tur bekler. Hedef kareye ulaş.";
            }
        }

        /// <summary>Null when there is nothing to show (offline, or the server did not accept the run).</summary>
        public static string Ranking(RunResult result) =>
            result == null || !result.Accepted
                ? null
                : $"Dünya sıralaması: {result.Rank}. / {result.Players} oyuncu (en iyin: {result.BestMoves} hamle)";

        // Touch devices have no keyboard: button labels drop the shortcut hints shown in App.uxml.
        public const string MenuButtonTouch = "Menü";
        public const string UndoButtonTouch = "Geri Al";
        public const string RestartButtonTouch = "Yeniden Başlat";
        public const string HintButtonTouch = "İpucu";

        public static string DirectionName(Direction direction)
        {
            switch (direction)
            {
                case Direction.Up: return "Yukarı";
                case Direction.Right: return "Sağ";
                case Direction.Down: return "Aşağı";
                case Direction.Left: return "Sol";
                case Direction.Wait: return "Bekle";
                default: throw new ArgumentOutOfRangeException(nameof(direction), direction, null);
            }
        }

        public static string LoadStatus(SaveLoadStatus status)
        {
            switch (status)
            {
                case SaveLoadStatus.RecoveredFromBackup: return "Kayıt dosyası bozuktu; son sağlam yedek yüklendi.";
                case SaveLoadStatus.CorruptedReset: return "Kayıt dosyası okunamadı; yeni bir kayıt başlatıldı.";
                case SaveLoadStatus.UnsupportedVersion: return "Kayıt dosyası oyunun daha yeni bir sürümüne ait. Dosyaya dokunulmadı; bu oturumda ilerleme kaydedilmeyecek.";
                default: return null;
            }
        }

        public static string ResumeDiscarded(ResumeDiscardReason reason)
        {
            switch (reason)
            {
                case ResumeDiscardReason.LevelUpdated: return "Yarım kalan bölüm güncellendi; bölüm baştan başlayacak.";
                case ResumeDiscardReason.LevelMissing: return "Yarım kalan bölüm artık mevcut değil.";
                case ResumeDiscardReason.InvalidSnapshot: return "Yarım kalan oyun okunamadı; bölüm baştan başlayacak.";
                default: return null;
            }
        }
    }
}
