namespace CarGenTools
{
    public enum ExitCode : int
    {
        Success = 0,
        Error = 1,

        BadCommandLine = 2,
        BadIO = 3,
        BadSaveData = 4,
        BadJson = 5,

        UnknownError = 255
    }
}
