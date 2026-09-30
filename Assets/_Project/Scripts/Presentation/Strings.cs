using System;
using OneMoreMove.Core;
using OneMoreMove.Session;
using UnityEngine;
using static OneMoreMove.Presentation.Localization;

namespace OneMoreMove.Presentation
{
    /// <summary>Every player-facing text in Turkish and English; see <see cref="Localization"/>.</summary>
    public static class Strings
    {
        // Main menu
        public static string GameTitle => L("Bir Hamle Daha", "One More Move");
        public static string Tagline => L("Her hamlenin bütün sonuçlarını düşün.", "Think through every consequence of every move.");
        public static string Continue => L("Devam Et", "Continue");
        public static string Levels => L("Bölümler", "Levels");
        public static string Settings => L("Ayarlar", "Settings");
        public static string Quit => L("Çıkış", "Quit");

        // Shared buttons
        public static string Back => L("Geri", "Back");
        public static string Close => L("Kapat", "Close");
        public static string Yes => L("Evet", "Yes");
        public static string No => L("Vazgeç", "Cancel");

        // HUD
        public static string Menu => L("Menü", "Menu");
        public static string Undo => L("Geri Al", "Undo");
        public static string Restart => L("Yeniden Başlat", "Restart");
        public static string HintLabel => L("İpucu", "Hint");
        public static string SpaceKey => L("Boşluk", "Space");

        /// <summary>Adds the keyboard shortcut on devices that have a keyboard ("Geri Al (Z)").</summary>
        public static string WithKey(string label, string key) => Application.isMobilePlatform ? label : $"{label} ({key})";

        public static string Moves(int count) => L($"Hamle: {count}", $"Moves: {count}");
        public static string UndoAvailable(int count) => L($"Geri alınabilir: {count}", $"Undo available: {count}");
        public static string LevelNumber(int index) => (index + 1).ToString();

        // Level select and win panel
        public static string Locked => L("Kilitli", "Locked");
        public static string Stars(int stars) => L($"{stars}/3 yıldız", $"{stars}/3 stars");
        public static string LevelComplete => L("Bölüm Tamamlandı", "Level Complete");
        public static string NextLevel => L("Sonraki Bölüm", "Next Level");
        public static string Replay => L("Tekrar Oyna", "Play Again");
        public static string AllLevelsComplete => L("Tüm bölümler tamamlandı!", "All levels complete!");
        public static string NewRecord => L("yeni rekor", "new record");
        public static string BestMoves(int? best) => L("En iyi: ", "Best: ") + (best.HasValue ? best.Value.ToString() : "-");
        public static string MovesResult(int moves, int par) => L($"Hamle: {moves}  (hedef {par})", $"Moves: {moves}  (target {par})");

        /// <summary>Null when there is nothing to show (offline, or the server did not accept the run).</summary>
        public static string Ranking(RunResult result) =>
            result == null || !result.Accepted
                ? null
                : L($"Dünya sıralaması: {result.Rank}. / {result.Players} oyuncu (en iyin: {result.BestMoves} hamle)",
                    $"World ranking: #{result.Rank} of {result.Players} players (your best: {result.BestMoves} moves)");

        // Dialogs and status
        public static string RestartConfirm => L("Bölüm baştan başlasın mı? Bu denemedeki hamleler silinir.",
            "Restart the level? The moves of this attempt are lost.");
        public static string HintThinking => L("İpucu hazırlanıyor…", "Preparing a hint…");

        // Settings
        public static string ReducedMotion => L("Animasyonları azalt", "Reduce motion");
        public static string MovePreview => L("Hamle önizlemesi", "Move preview");
        public static string MovePreviewHelp => L(
            "Açıkken bir yön düğmesine basılıyken ya da imleç üzerindeyken taşların gideceği yer gösterilir.",
            "When on, pressing or hovering a direction button shows where the pieces would go.");
        public static string TextSize => L("Yazı boyutu", "Text size");
        public static string TextScale(int percent) => L($"%{percent}", $"{percent}%");
        public static string SfxVolume => L("Efekt sesi", "Sound effects");
        public static string MusicVolume => L("Müzik", "Music");
        public static string LanguageLabel => L("Dil", "Language");

        // Screen reader
        public static string On => L("açık", "on");
        public static string Off => L("kapalı", "off");
        public static string ChangeHint => L("Değiştirmek için iki kez dokun.", "Double-tap to change.");

        public static string Reject(RejectReason reason)
        {
            switch (reason)
            {
                case RejectReason.OutOfBounds: return L("Tahtanın dışına çıkılamaz", "You cannot leave the board");
                case RejectReason.Wall: return L("Duvar var", "A wall is in the way");
                case RejectReason.GateClosed: return L("Kapı kapalı", "The gate is closed");
                case RejectReason.BlockedByEcho: return L("Yankı yolu kapatıyor", "The echo is in the way");
                case RejectReason.LevelAlreadyWon: return L("Bölüm zaten tamamlandı", "The level is already complete");
                default: return L("Bu hamle yapılamaz", "This move is not possible");
            }
        }

        public static string EchoBlocked(EchoBlockReason reason)
        {
            switch (reason)
            {
                case EchoBlockReason.GateClosed:
                    return L("Yankının önünde kapalı kapı var, yerinde kaldı", "A closed gate blocks the echo; it stays put");
                case EchoBlockReason.BlockedByPlayer:
                    return L("Yankı sana çarptı, yerinde kaldı", "The echo bumped into you; it stays put");
                default:
                    return L("Yankının yolu kapalı, yerinde kaldı", "The echo's way is blocked; it stays put");
            }
        }

        public static string HintText(Hint hint)
        {
            switch (hint.Kind)
            {
                case HintKind.MechanicReminder: return Reminder(hint.Topic);
                case HintKind.SuggestedDirection:
                    return hint.SuggestedDirection == Direction.Wait
                        ? L($"Önerilen: bir tur bekle (hedefe en az {hint.MovesToGoal} hamle)",
                            $"Suggested: wait one turn (at least {hint.MovesToGoal} moves to the goal)")
                        : L($"Önerilen yön: {DirectionName(hint.SuggestedDirection ?? Direction.Up)} (hedefe en az {hint.MovesToGoal} hamle)",
                            $"Suggested direction: {DirectionName(hint.SuggestedDirection ?? Direction.Up)} (at least {hint.MovesToGoal} moves to the goal)");
                case HintKind.SuggestUndo:
                    return L("Bu durumdan hedefe ulaşılamıyor. Geri almayı ya da yeniden başlatmayı dene.",
                        "The goal cannot be reached from here. Try undo or restart.");
                case HintKind.AlreadySolved: return L("Bölüm tamamlandı.", "Level complete.");
                default: return L("İpucu hazırlanamadı.", "The hint could not be prepared.");
            }
        }

        public static string Reminder(MechanicTopic topic)
        {
            switch (topic)
            {
                case MechanicTopic.Gates:
                    return L("Hatırlatma: Her başarılı hamlede bütün kapılar açılır ya da kapanır. Kapalı kapıya girilemez; üstündeki taş ise çıkabilir. Beklemek de bir hamledir ve kapıları çevirir.",
                        "Reminder: every successful move opens or closes every gate. You cannot enter a closed gate, but a piece standing on one can leave it. Waiting is a move too and flips the gates.");
                case MechanicTopic.Echo:
                    return L("Hatırlatma: Yankı her hamlede senin yönünün tersine gider. Önü kapalıysa yerinde kalır; onun bulunduğu kareye giremezsin.",
                        "Reminder: the echo moves opposite to you on every move. When its way is blocked it stays put, and you cannot enter its cell.");
                default:
                    return L("Hatırlatma: Taşın dört yönden birine bir kare gider ya da bir tur bekler. Hedef kareye ulaş.",
                        "Reminder: your piece moves one cell in one of four directions or waits a turn. Reach the goal.");
            }
        }

        public static string DirectionName(Direction direction)
        {
            switch (direction)
            {
                case Direction.Up: return L("Yukarı", "Up");
                case Direction.Right: return L("Sağ", "Right");
                case Direction.Down: return L("Aşağı", "Down");
                case Direction.Left: return L("Sol", "Left");
                case Direction.Wait: return L("Bekle", "Wait");
                default: throw new ArgumentOutOfRangeException(nameof(direction), direction, null);
            }
        }

        public static string LoadStatus(SaveLoadStatus status)
        {
            switch (status)
            {
                case SaveLoadStatus.RecoveredFromBackup:
                    return L("Kayıt dosyası bozuktu; son sağlam yedek yüklendi.", "The save file was damaged; the last good backup was loaded.");
                case SaveLoadStatus.CorruptedReset:
                    return L("Kayıt dosyası okunamadı; yeni bir kayıt başlatıldı.", "The save file could not be read; a new save was started.");
                case SaveLoadStatus.UnsupportedVersion:
                    return L("Kayıt dosyası oyunun daha yeni bir sürümüne ait. Dosyaya dokunulmadı; bu oturumda ilerleme kaydedilmeyecek.",
                        "The save file belongs to a newer version of the game. It was left untouched; progress will not be saved this session.");
                default: return null;
            }
        }

        public static string ResumeDiscarded(ResumeDiscardReason reason)
        {
            switch (reason)
            {
                case ResumeDiscardReason.LevelUpdated:
                    return L("Yarım kalan bölüm güncellendi; bölüm baştan başlayacak.", "The unfinished level was updated; it starts over.");
                case ResumeDiscardReason.LevelMissing:
                    return L("Yarım kalan bölüm artık mevcut değil.", "The unfinished level no longer exists.");
                case ResumeDiscardReason.InvalidSnapshot:
                    return L("Yarım kalan oyun okunamadı; bölüm baştan başlayacak.", "The unfinished game could not be read; the level starts over.");
                default: return null;
            }
        }
    }
}
