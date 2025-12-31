using System.Collections.Generic;
using System.Threading.Tasks;
using KeganOS.Core.Models;

namespace KeganOS.Core.Interfaces
{
    public interface INoteService
    {
        Task<List<NoteItem>> GetNotesAsync(int userId);
        Task<NoteItem> GetNoteByIdAsync(string noteId);
        Task SaveNoteAsync(int userId, NoteItem note);
        Task DeleteNoteAsync(string noteId);
        Task DeleteNotesAsync(IEnumerable<string> noteIds);
        Task<List<NoteItem>> SearchNotesAsync(int userId, string query);
        Task<List<NoteItem>> GetPinnedNotesAsync(int userId);
    }
}
