function readProperties(formatInstance) {
    let properties = [];
    for (let property of formatInstance.properties) {
        let value = formatInstance.readProperty(property[0]);
        if (value != null) {
            properties.push({
                id: property[0],
                type: property[1],
                value: value,
            });
        }
    }
    return properties;
}

function readImageProperties(formatInstance) {
    let properties = [];
    for (let property of formatInstance.imageProperties) {
        let value = formatInstance.readImageProperty(property[0]);
        if (value != null) {
            properties.push({
                id: property[0],
                type: property[1],
                value: value,
            });
        }
    }
    return properties;
}

function readImage(path, inputHints) {
    let fileFormat = new FileFormat(File.extractExtension(path).toLowerCase(), true, false);
    let formatInstance = new FileFormatInstance(fileFormat);
    try {
        let description = formatInstance.open(path, inputHints);
        if (description.length != 1) {
            throw new Error("Unknown image format");
        }

        let image = new Image(1, 1, description.numberOfChannels || 1, 0 /*ColorSpace_Gray*/, description[0].bitsPerSample, description[0].ieeefpSampleFormat ? 1 /*SampleType_Real*/ : 0 /*SampleType_Integer*/);

        if (!formatInstance.readImage(image)) {
            throw new Error("Failed reading file: " + path);
        }

        return {
            image: image,
            description: description[0],
            keywords: fileFormat.canStoreKeywords ? formatInstance.keywords : undefined,
            iccProfile: fileFormat.canStoreICCProfiles ? formatInstance.iccProfile : undefined,
            properties: fileFormat.canStoreProperties ? readProperties(formatInstance) : undefined,
            imageProperties: fileFormat.canStoreImageProperties ? readImageProperties(formatInstance) : undefined,
        };
    } finally {
        formatInstance.close();
    }
}

function writeProperties(image, formatInstance) {
    for (let property of image.properties) {
        if (!formatInstance.writeProperty(property.id, property.value, property.type)) {
            throw new Error("Failed writing property: " + JSON.stringify(property));
        }
    }
}

function writeImageProperties(image, formatInstance) {
    for (let property of image.imageProperties) {
        if (!formatInstance.writeImageProperty(property.id, property.value, property.type)) {
            throw new Error("Failed writing image property: " + JSON.stringify(property));
        }
    }
}

function writeImage(image, path, outputHints) {
    let fileFormat = new FileFormat(File.extractExtension(path).toLowerCase(), false, true);
    let formatInstance = new FileFormatInstance(fileFormat);
    try {
        if (!formatInstance.create(path, outputHints)) {
            throw new Error("Failed creating file: " + path);
        }

        let description = new ImageDescription(image.description);

        if (!formatInstance.setOptions(description)) {
            throw new Error("Failed setting output options");
        }

        if (fileFormat.canStoreKeywords && image.keywords != undefined) {
            formatInstance.keywords = image.keywords;
        }

        if (fileFormat.canStoreICCProfiles && image.iccProfile != undefined) {
            formatInstance.iccProfile = image.iccProfile;
        }

        if (fileFormat.canStoreProperties && image.properties != undefined) {
            writeProperties(image, formatInstance);
        }

        if (fileFormat.canStoreImageProperties && image.imageProperties != undefined) {
            writeImageProperties(image, formatInstance);
        }

        if (!formatInstance.writeImage(image.image)) {
            throw new Error("Failed writing file: " + path);
        }
    } finally {
        formatInstance.close();
    }
}

function parseFiles(args) {
    let files = [];

    for (let i = 0; i < args.length; i += 2) {
        let path = args[i];

        let keywords = [];

        if (i + 1 < args.length) {
            let keywordArgs = ByteArray.fromBase64(args[i + 1]).utf8ToString().split('\n');

            for (let j = 0; j + 2 < keywordArgs.length; j += 3) {
                let key = ByteArray.fromBase64(keywordArgs[j]).utf8ToString();
                let value = ByteArray.fromBase64(keywordArgs[j + 1]).utf8ToString();
                let comment = ByteArray.fromBase64(keywordArgs[j + 2]).utf8ToString();

                if (comment == "") {
                    comment = null;
                }

                keywords.push({
                    key: key,
                    value: value,
                    comment: comment,
                });
            }
        }

        files.push({
            path: path,
            keywords: keywords,
        })
    }

    return files;
}

function main() {
    let settings = {
        outputExtension: args[0].toLowerCase(),
        inputFiles: parseFiles(args.slice(1)),
        inputHints: "raw cfa",
        outputHints: "",
    };

    console.writeln();
    console.noteln("===== AutoFlats: Convert =====");
    console.writeln();
    console.writeln(JSON.stringify(settings, null, 2));
    console.writeln();

    if (settings.outputExtension.indexOf(".") < 0) {
        console.criticalln("*** Error: Invalid output extension " + settings.outputExtension);
        return;
    }

    for (let file of settings.inputFiles) {
        console.noteln("* Converting file: " + file.path);

        let image = readImage(file.path, settings.inputHints);

        try {
            for (let keyword of file.keywords) {
                image.keywords.push(new FITSKeyword(keyword.key, keyword.value, keyword.comment));
            }

            let outputFile = file.path.substring(0, file.path.length - File.extractExtension(file.path).length) + settings.outputExtension;

            writeImage(image, outputFile, settings.outputHints);
        } finally {
            image.image.free();
        }

        console.writeln();
    }
}

try {
    main();
    writeResult();
} catch (e) {
    writeResult("ERROR: " + e.toString());
}
