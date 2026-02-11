using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MiRoti.Data;
using MiRoti.Models;
using Microsoft.AspNetCore.Authorization;

namespace MiRoti.Controllers
{
    [Authorize(Roles = "Admin,Cocinero")]
    public class IngredientesController : Controller
    {
        private readonly MiRotiContext _context;

        public IngredientesController(MiRotiContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            var ingredientes = await _context.Ingredientes
                .Include(i => i.UnidadMedida)
                .Where(i => i.Nombre != null && i.Nombre != "")
                .OrderBy(i => i.Nombre)
                .ToListAsync();

            return View("~/Views/Shared/IngredientesIndex.cshtml", ingredientes);
        }

        public async Task<IActionResult> Create()
        {
            ViewBag.UnidadesMedida = await _context.UnidadesMedida
                .Where(u => u.Nombre != null && u.Nombre != "" && u.Abreviatura != null && u.Abreviatura != "")
                .ToListAsync();
            return View("~/Views/Shared/IngredientesCreate.cshtml");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Ingrediente ingrediente)
        {
            // Validaciones adicionales del lado del servidor
            if (string.IsNullOrWhiteSpace(ingrediente.Nombre))
            {
                ModelState.AddModelError("Nombre", "El nombre del ingrediente es obligatorio");
            }

            if (ingrediente.CostoUnitario <= 0)
            {
                ModelState.AddModelError("CostoUnitario", "El costo debe ser mayor a 0");
            }

            if (ingrediente.UnidadMedidaId <= 0)
            {
                ModelState.AddModelError("UnidadMedidaId", "Debe seleccionar una unidad de medida");
            }
            else
            {
                // Verificar que la unidad de medida existe
                var unidadExiste = await _context.UnidadesMedida.AnyAsync(u => u.Id == ingrediente.UnidadMedidaId);
                if (!unidadExiste)
                {
                    ModelState.AddModelError("UnidadMedidaId", "La unidad de medida seleccionada no es válida");
                }
            }

            if (ModelState.IsValid)
            {
                _context.Ingredientes.Add(ingrediente);
                await _context.SaveChangesAsync();
                TempData["Success"] = $"Ingrediente '{ingrediente.Nombre}' agregado correctamente";
                return RedirectToAction(nameof(Index));
            }

            // Mostrar errores específicos al usuario
            var errores = string.Join(", ", ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage));
            TempData["Error"] = $"Error al agregar el ingrediente: {errores}";

            ViewBag.UnidadesMedida = await _context.UnidadesMedida
                .Where(u => u.Nombre != null && u.Nombre != "" && u.Abreviatura != null && u.Abreviatura != "")
                .ToListAsync();
            return View("~/Views/Shared/IngredientesCreate.cshtml", ingrediente);
        }

        [HttpPost]
        public IActionResult CreateJson([FromBody] Ingrediente ingrediente)
        {
            if (ingrediente == null)
                return Json(new { success = false, message = "Datos inválidos" });

            if (string.IsNullOrWhiteSpace(ingrediente.Nombre))
                return Json(new { success = false, message = "El nombre es obligatorio" });

            if (ingrediente.UnidadMedidaId <= 0)
                return Json(new { success = false, message = "Debe seleccionar una unidad de medida" });

            if (ingrediente.CostoUnitario <= 0)
                return Json(new { success = false, message = "El costo debe ser mayor a 0" });

            _context.Ingredientes.Add(ingrediente);
            _context.SaveChanges();

            return Json(new { success = true });
        }

        public async Task<IActionResult> Edit(int id)
        {
            var ingrediente = await _context.Ingredientes
                .Include(i => i.UnidadMedida)
                .FirstOrDefaultAsync(i => i.Id == id);

            if (ingrediente == null)
                return NotFound();

            return View("~/Views/Shared/IngredientesEdit.cshtml", ingrediente);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, decimal costoUnitario)
        {
            var ingrediente = await _context.Ingredientes.FindAsync(id);
            if (ingrediente == null)
                return NotFound();

            if (costoUnitario <= 0)
            {
                TempData["Error"] = "El costo debe ser mayor a 0";
                return RedirectToAction(nameof(Edit), new { id });
            }

            ingrediente.CostoUnitario = costoUnitario;
            
            try
            {
                _context.Update(ingrediente);
                await _context.SaveChangesAsync();
                TempData["Success"] = $"Precio de {ingrediente.Nombre} actualizado correctamente";
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Error al actualizar el precio";
            }

            return RedirectToAction(nameof(Index));
        }

        // Método temporal para limpiar y recrear datos base
        [HttpGet]
        public async Task<IActionResult> FixLegacyData()
        {
            // PASO 1: Eliminar TODOS los ingredientes (para evitar problemas de FK)
            var todosIngredientes = await _context.Ingredientes.ToListAsync();
            _context.Ingredientes.RemoveRange(todosIngredientes);
            await _context.SaveChangesAsync();

            // PASO 2: Eliminar TODAS las unidades de medida
            var todasUnidades = await _context.UnidadesMedida.ToListAsync();
            _context.UnidadesMedida.RemoveRange(todasUnidades);
            await _context.SaveChangesAsync();

            // PASO 3: Recrear las 4 unidades básicas
            var unidades = new[]
            {
                new UnidadMedida { Nombre = "Kilogramo", Abreviatura = "kg" },
                new UnidadMedida { Nombre = "Gramo", Abreviatura = "g" },
                new UnidadMedida { Nombre = "Unidad", Abreviatura = "u" },
                new UnidadMedida { Nombre = "Litro", Abreviatura = "L" }
            };
            _context.UnidadesMedida.AddRange(unidades);
            await _context.SaveChangesAsync();

            // PASO 4: Recrear ingredientes base
            var ingredientes = new[]
            {
                new Ingrediente { Nombre = "Papa", CostoUnitario = 150, UnidadMedidaId = unidades[1].Id },      // Gramo
                new Ingrediente { Nombre = "Pollo", CostoUnitario = 1200, UnidadMedidaId = unidades[0].Id },    // Kilogramo
                new Ingrediente { Nombre = "Aceite", CostoUnitario = 900, UnidadMedidaId = unidades[3].Id },    // Litro
                new Ingrediente { Nombre = "Huevo", CostoUnitario = 80, UnidadMedidaId = unidades[2].Id }       // Unidad
            };
            _context.Ingredientes.AddRange(ingredientes);
            await _context.SaveChangesAsync();

            TempData["Success"] = "Base de datos limpiada y recreada exitosamente. 4 unidades y 4 ingredientes base creados.";
            return RedirectToAction(nameof(Index));
        }
    }
}
