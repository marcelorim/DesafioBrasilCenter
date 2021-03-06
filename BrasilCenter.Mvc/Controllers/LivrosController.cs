﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using BrasilCenter.Business.Models;
using BrasilCenter.Mvc.Data;
using Microsoft.AspNetCore.Authorization;
using System.IO;
using Microsoft.AspNetCore.Hosting;
using System.Net.Http.Headers;
using Microsoft.AspNetCore.Http;

namespace BrasilCenter.Mvc.Controllers
{
    [Authorize]
    public class LivrosController : Controller
    {
        private readonly ApplicationDbContext _context;

        private IHostingEnvironment _env;

        public LivrosController(ApplicationDbContext context, IHostingEnvironment env)
        {
            _context = context;
            _env = env;
        }

        public async Task<IActionResult> Index(string sortOrder, string filterList, string searchString)
        {
            var list = await _context.Livros.ToListAsync();

            if (!string.IsNullOrEmpty(filterList) && !string.IsNullOrEmpty(searchString))
                list = ObterListaFiltrada(filterList, searchString, list);

            list = ObterListaOrdenada(sortOrder, list);

            return View(list);
        }

        private static List<Livro> ObterListaFiltrada(string filterList, string searchString, List<Livro> list)
        {
            switch (filterList)
            {
                case "Nome":
                    list = list.Where(s => s.Nome.Contains(searchString)).ToList();
                    break;
                case "Isbn":
                    if (searchString.Where(c => char.IsNumber(c)).Count() > 0)
                    {
                        list = list.Where(s => s.Isbn == int.Parse(searchString)).ToList();
                    }
                    break;
                case "Autor":
                    list = list.Where(s => s.Autor.Contains(searchString)).ToList();
                    break;
            }

            return list;
        }

        private List<Livro> ObterListaOrdenada(string sortOrder, List<Livro> list)
        {
            ViewBag.NomeSortParm = string.IsNullOrEmpty(sortOrder) ? "nome_desc" : "";
            ViewBag.IsbnSortParm = sortOrder == "Isbn" ? "isbn_desc" : "Isbn";
            ViewBag.AutorSortParm = sortOrder == "Autor" ? "autor_desc" : "Autor";
            ViewBag.PrecoSortParm = sortOrder == "Preco" ? "preco_desc" : "Preco";
            ViewBag.DataPubSortParm = sortOrder == "Data" ? "data_desc" : "Data";

            switch (sortOrder)
            {
                case "nome_desc":
                    list = list.OrderByDescending(s => s.Nome).ToList();
                    break;
                case "Isbn":
                    list = list.OrderBy(s => s.Isbn).ToList();
                    break;
                case "isbn_desc":
                    list = list.OrderByDescending(s => s.Isbn).ToList();
                    break;
                case "Autor":
                    list = list.OrderBy(s => s.Autor).ToList();
                    break;
                case "autor_desc":
                    list = list.OrderByDescending(s => s.Autor).ToList();
                    break;
                case "Preco":
                    list = list.OrderBy(s => s.Preco).ToList();
                    break;
                case "preco_desc":
                    list = list.OrderByDescending(s => s.Preco).ToList();
                    break;
                case "Data":
                    list = list.OrderBy(s => s.DtPublicacao).ToList();
                    break;
                case "data_desc":
                    list = list.OrderByDescending(s => s.DtPublicacao).ToList();
                    break;
                default:
                    list = list.OrderBy(s => s.Nome).ToList();
                    break;
            }

            return list;
        }

        public async Task<IActionResult> Details(Guid? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var livro = await _context.Livros
                .FirstOrDefaultAsync(m => m.Id == id);
            if (livro == null)
            {
                return NotFound();
            }

            return View(livro);
        }

        public IActionResult Create()
        {
            return View();
        }



        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Livro livro)
        {
            if (ModelState.IsValid)
            {
                bool isbnExiste = _context.Livros.AsNoTracking().Any(i => i.Isbn == livro.Isbn);

                if (isbnExiste)
                {
                    ModelState.AddModelError("error", "Isbn já cadastrado!");
                }
                else
                {
                    if (!await UploadArquivo(livro))
                        return View(livro);

                    _context.Add(livro);
                    await _context.SaveChangesAsync();
                    return RedirectToAction(nameof(Index));
                }
            }
            return View(livro);
        }

        private async Task<bool> UploadArquivo(Livro livro)
        {
            var webRoot = _env.WebRootPath;
            var caminhoUpload = Path.Combine(webRoot, "uploads\\capas_livros");

            if (!Directory.Exists(caminhoUpload))
            {
                Directory.CreateDirectory(caminhoUpload);
            }

            IFormFileCollection arquivos = HttpContext.Request.Form.Files;
            foreach (var img in arquivos)
            {
                if (img != null && img.Length > 0)
                {
                    var arquivo = img;
                    if (arquivo.Length > 0)
                    {
                        var novoNomeArquivo = Guid.NewGuid().ToString() + "_" + arquivo.FileName;
                        var pathCompleto = Path.Combine(caminhoUpload, novoNomeArquivo);

                        if (System.IO.File.Exists(pathCompleto))
                        {
                            ModelState.AddModelError(key: string.Empty, errorMessage: "Já existe um arquivo com esse nome!");
                            return false;
                        }

                        using (var fileStream = new FileStream(Path.Combine(caminhoUpload, novoNomeArquivo), FileMode.Create))
                        {
                            await arquivo.CopyToAsync(fileStream);
                            livro.Capa = novoNomeArquivo;
                        }

                    }
                }
            }

            return true;
        }

        public async Task<IActionResult> Edit(Guid? id)
        {
            if (id == null) return NotFound();

            var livro = await _context.Livros.FindAsync(id);
            if (livro == null) return NotFound();

            return View(livro);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(Guid id, Livro livro)
        {
            if (id != livro.Id)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    if (!await UploadArquivo(livro))
                        return View(livro);

                    _context.Update(livro);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!LivroExists(livro.Id))
                    {
                        return NotFound();
                    }
                    else
                    {
                        throw;
                    }
                }
                return RedirectToAction(nameof(Index));
            }
            return View(livro);
        }

        public async Task<IActionResult> Delete(Guid? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var livro = await _context.Livros
                .FirstOrDefaultAsync(m => m.Id == id);
            if (livro == null)
            {
                return NotFound();
            }

            return View(livro);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(Guid id)
        {
            var livro = await _context.Livros.FindAsync(id);
            _context.Livros.Remove(livro);
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        private bool LivroExists(Guid id)
        {
            return _context.Livros.Any(e => e.Id == id);
        }

    }
}
