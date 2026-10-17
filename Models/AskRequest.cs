namespace MetroTechAssistant.Models
{
    public class AskRequest
    {
        public string Question { get; set; }
        public string Language { get; set; } = "en"; 
        public string? ImageBase64 { get; set; }
    }
}
