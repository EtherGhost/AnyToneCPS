using System.Threading;
using System.Threading.Tasks;
using AnyToneCPS.Models;

namespace AnyToneCPS.Services;

public interface IRadioDataStore
{
    string DisplayName { get; }
    string Location { get; }
    Task<RadioProjectData?> LoadAsync(CancellationToken cancellationToken = default);
    Task SaveAsync(RadioProjectData project, CancellationToken cancellationToken = default);
}
