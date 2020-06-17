namespace Hubbup.Web.Diagnostics.Telemetry
{
    public interface IRequestTelemetryFeature
    {
        /// <summary>
        /// Add a property to the telemetry record for this request.
        /// </summary>
        /// <param name="name">The name of the property to add</param>
        /// <param name="value">The value of the property to add</param>
        void AddProperty(string name, object value);
    }
}
