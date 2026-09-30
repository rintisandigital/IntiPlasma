namespace Domain.Partnership.Cycles;

public enum CycleStatus
{
    /// <summary>
    /// Rencana chick-in.
    /// </summary>
    Planned = 1,

    /// <summary>
    /// DOC sudah chick-in, pemeliharaan berjalan.
    /// </summary>
    Active = 2,

    /// <summary>
    /// Panen berjalan.
    /// </summary>
    Harvesting = 3,

    /// <summary>
    /// Populasi habis, siklus ditutup (siap settlement).
    /// </summary>
    Closed = 4,

    /// <summary>
    /// Settlement plasma selesai; siklus terkunci.
    /// </summary>
    Settled = 5,

    Cancelled = 9
}
