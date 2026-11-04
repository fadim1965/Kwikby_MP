using System.ComponentModel.DataAnnotations;

namespace kwikby_MP.Areas.Vendor.Models;

    /// <summary>
    /// Coordinates transaction parameters between layout picking grids and backend server layers.
    /// </summary>
    public class AppearanceViewModel
    {
        public int ShopId { get; set; }

        public string ShopName { get; set; } = string.Empty;

        public string CurrentThemeKey { get; set; } = string.Empty;

        [Required(ErrorMessage = "Please make an intentional design palette select selection before submitting.")]
        [StringLength(50, ErrorMessage = "Selected design signature token length boundary overflow.")]
        public string SelectedThemeKey { get; set; } = string.Empty;
    }


