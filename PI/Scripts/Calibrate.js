function createProcess() {
    var P = new ImageCalibration;
    P.targetFrames = [ // enabled, path
    ];
    P.enableCFA = true;
    P.cfaPattern = ImageCalibration.prototype.Auto;
    P.inputHints = "fits-keywords normalize raw cfa signed-is-physical";
    P.outputHints = "properties fits-keywords no-compress-data no-embedded-data no-resolution";
    P.pedestal = 0;
    P.pedestalMode = ImageCalibration.prototype.Keyword;
    P.pedestalKeyword = "";
    P.overscanEnabled = false;
    P.overscanImageX0 = 0;
    P.overscanImageY0 = 0;
    P.overscanImageX1 = 0;
    P.overscanImageY1 = 0;
    P.overscanRegions = [ // enabled, sourceX0, sourceY0, sourceX1, sourceY1, targetX0, targetY0, targetX1, targetY1
        [false, 0, 0, 0, 0, 0, 0, 0, 0],
        [false, 0, 0, 0, 0, 0, 0, 0, 0],
        [false, 0, 0, 0, 0, 0, 0, 0, 0],
        [false, 0, 0, 0, 0, 0, 0, 0, 0]
    ];
    P.masterBiasEnabled = true;
    P.masterBiasPath = "";
    P.masterDarkEnabled = true;
    P.masterDarkPath = "";
    P.masterFlatEnabled = true;
    P.masterFlatPath = "";
    P.calibrateBias = false;
    P.calibrateDark = false;
    P.calibrateFlat = false;
    P.optimizeDarks = true;
    P.darkOptimizationThreshold = 0.00000;
    P.darkOptimizationLow = 3.0000;
    P.darkOptimizationWindow = 0;
    P.darkCFADetectionMode = ImageCalibration.prototype.DetectCFA;
    P.separateCFAFlatScalingFactors = true;
    P.flatScaleClippingFactor = 0.05;
    P.cosmeticCorrectionLow = false;
    P.cosmeticLowSigma = 5;
    P.cosmeticCorrectionHigh = false;
    P.cosmeticHighSigma = 10;
    P.cosmeticKernelRadius = 1;
    P.cosmeticShowMap = false;
    P.cosmeticShowMapAndStop = false;
    P.evaluateNoise = true;
    P.noiseEvaluationAlgorithm = ImageCalibration.prototype.NoiseEvaluation_MRS;
    P.evaluateSignal = true;
    P.structureLayers = 5;
    P.saturationThreshold = 1.00;
    P.saturationRelative = false;
    P.noiseLayers = 1;
    P.hotPixelFilterRadius = 1;
    P.noiseReductionFilterRadius = 0;
    P.minStructureSize = 0;
    P.psfType = ImageCalibration.prototype.PSFType_Moffat4;
    P.psfGrowth = 1.00;
    P.maxStars = 24576;
    P.outputDirectory = "";
    P.outputExtension = ".xisf";
    P.outputPrefix = "";
    P.outputPostfix = "_c";
    P.outputSampleFormat = ImageCalibration.prototype.f32;
    P.outputPedestal = 0;
    P.outputPedestalMode = ImageCalibration.prototype.OutputPedestal_Literal;
    P.autoPedestalLimit = 0.00010;
    P.overwriteExistingFiles = false;
    P.onError = ImageCalibration.prototype.Continue;
    P.noGUIMessages = true;
    P.useFileThreads = true;
    P.fileThreadOverload = 1.00;
    P.maxFileReadThreads = 0;
    P.maxFileWriteThreads = 0;
    /*
     * Read-only properties
     *
    P.outputData = [ // outputFilePath, darkScalingFactorRK, darkScalingFactorG, darkScalingFactorB, psfTotalFluxEstimateRK, psfTotalFluxEstimateG, psfTotalFluxEstimateB, psfTotalPowerFluxEstimateRK, psfTotalPowerFluxEstimateG, psfTotalPowerFluxEstimateB, psfTotalMeanFluxEstimateRK, psfTotalMeanFluxEstimateG, psfTotalMeanFluxEstimateB, psfTotalMeanPowerFluxEstimateRK, psfTotalMeanPowerFluxEstimateG, psfTotalMeanPowerFluxEstimateB, psfMStarEstimateRK, psfMStarEstimateG, psfMStarEstimateB, psfNStarEstimateRK, psfNStarEstimateG, psfNStarEstimateB, psfCountRK, psfCountG, psfCountB, noiseEstimateRK, noiseEstimateG, noiseEstimateB, noiseFractionRK, noiseFractionG, noiseFractionB, noiseScaleLowRK, noiseScaleLowG, noiseScaleLowB, noiseScaleHighRK, noiseScaleHighG, noiseScaleHighB, noiseAlgorithmRK, noiseAlgorithmG, noiseAlgorithmB, cosmeticCorrectionLowCountRK, cosmeticCorrectionLowCountG, cosmeticCorrectionLowCountB, cosmeticCorrectionHighCountRK, cosmeticCorrectionHighCountG, cosmeticCorrectionHighCountB
    ];
    P.cosmeticCorrectionMapId = "";
     */
    return P;
}

function configureProcess(process, settings) {
    process.targetFrames = settings.inputFiles.map((file) => [true, file]);
    process.enableCFA = false;
    process.masterBiasEnabled = false;
    process.masterFlatEnabled = settings.masterFlatFile != "";
    process.masterDarkEnabled = true;
    process.masterDarkPath = settings.masterDarkFile;
    process.masterFlatPath = settings.masterFlatFile;
    process.optimizeDarks = false;
    process.cosmeticCorrectionLow = settings.cosmeticCorrection;
    process.cosmeticCorrectionHigh = settings.cosmeticCorrection;
    process.outputDirectory = settings.outputDirectory;
    process.outputPrefix = "";
    process.outputPostfix = "";
    process.outputSampleFormat = settings.outputFormat;
    process.overwriteExistingFiles = true;
}

function main() {
    let settings = {
        outputDirectory: args[0],
        outputFormat: args[1],
        cosmeticCorrection: args[2] == true,
        masterDarkFile: args[3],
        masterFlatFile: args[4],
        inputFiles: args.slice(5),
    };

    console.writeln();
    console.noteln("===== AutoFlats: Calibrate =====");
    console.writeln();
    console.writeln(JSON.stringify(settings, null, 2));

    switch (settings.outputFormat) {
        case "i16":
            settings.outputFormat = ImageCalibration.prototype.i16;
            break;
        case "i32":
            settings.outputFormat = ImageCalibration.prototype.i32;
            break;
        case "f32":
            settings.outputFormat = ImageCalibration.prototype.f32;
            break;
        case "f64":
            settings.outputFormat = ImageCalibration.prototype.f64;
            break;
        default:
            console.writeln();
            console.criticalln("*** Error: Invalid output format " + settings.outputFormat);
            throw new Error("Invalid output format " + settings.outputFormat);
            return;
    }

    let process = createProcess();

    configureProcess(process, settings);

    let processSuccess = false;
    let processLog = "";
    console.beginLog();
    try {
        processSuccess = process.executeGlobal();
    } finally {
        processLog = console.endLog().utf8ToString();
    }

    if (!processSuccess) {
        console.criticalln("*** Error: ImageCalibration process failed");
        throw new Error("ImageCalibration process failed: \n" + processLog);
    }
}

try {
    main();
    writeResult();
} catch (e) {
    writeResult("ERROR: " + e.toString());
}
