/*===============================================================================================*/

let args = File.readLines(ByteArray.fromBase64(jsArguments[0] /* input file */).utf8ToString())
    .map((arg) => ByteArray.fromBase64(arg).utf8ToString());

function writeResult(result, status = "finished") {
    function writeToFile(file, content) {
        for (let i = 0; i <= 6; ++i) {
            try {
                File.writeTextFile(file, content);
                return;
            } catch (e) {
                sleep((Math.pow(2, i) + Math.random()) * 0.1);
                continue;
            }
        }
        File.writeTextFile(file, content);
    }
    if (typeof result === 'undefined') {
        result = "";
    }
    if (status === "finished") {
        writeToFile(ByteArray.fromBase64(jsArguments[1] /* result file */).utf8ToString(), result);
    }
    writeToFile(ByteArray.fromBase64(jsArguments[2] /* status file */).utf8ToString(), status);
}

writeResult(undefined, "started");

/*===============================================================================================*/
