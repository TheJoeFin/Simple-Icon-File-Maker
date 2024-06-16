namespace Simple_Icon_File_Maker.Contracts.Services;

public interface IActivationService
{
    Task ActivateAsync(object activationArgs);
}
