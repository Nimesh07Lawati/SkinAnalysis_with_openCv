using System;
using SkinCareAiIntegration.Models;

namespace SkinCareAiIntegration.Services;


    public interface IDetectService
    {
        DetectResult Detect(byte[] file);
    }
