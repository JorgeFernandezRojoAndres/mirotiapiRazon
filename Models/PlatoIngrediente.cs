using System.ComponentModel.DataAnnotations.Schema;

namespace MiRoti.Models
{
    [Table("PlatoIngrediente")]
    public class PlatoIngrediente
    {
        public int PlatoId { get; set; }
        public Plato Plato { get; set; } = null!;

        public int IngredienteId { get; set; }
        public Ingrediente Ingrediente { get; set; } = null!;

        public double Cantidad { get; set; }

        [Column(TypeName = "decimal(10,2)")]
        public decimal Subtotal { get; set; }
    }
}
