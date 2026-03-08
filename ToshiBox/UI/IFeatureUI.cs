namespace ToshiBox.UI
{
    public interface IFeatureUI
    {
        string Name { get; }
        bool Enabled { get; set; }
        void DrawSettings();
    }
}