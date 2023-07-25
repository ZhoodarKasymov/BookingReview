// Please see documentation at https://docs.microsoft.com/aspnet/core/client-side/bundling-and-minification
// for details on configuring this project to bundle and minify static web assets.

// Write your JavaScript code.

function dangerAlert(message) {
    let alertElement = $(".alert");
    alertElement.show();
    alertElement.empty();
    alertElement.append(message);
    alertElement.fadeTo(3000, 500).slideUp(500, function(){
        $(".alert").slideUp(500);
    });
}

$(document).ready(function () {
    $('.validate-input').change(function () {
        validateInput($(this));
    });

    $('#validateButton').click(function () {
        $('.validate-input').each(function () {
            validateInput($(this));
        });
    });
});

function validateInput(input) {
    let value = input.val();
    let pattern = /^[A-Za-z]+\d+$/;

    if (value === '') {
        input.removeClass('error');
        $('.error-message').remove();
        input.addClass('error');
        input.after('<div class="error-message">Это поле не может быть пустым.</div>');
    } else if (!pattern.test(value)) {
        input.removeClass('error');
        $('.error-message').remove();
        input.addClass('error');
        input.after('<div class="error-message">Не правильное значение талона!</div>');
    } else {
        input.removeClass('error');
        $('.error-message').remove();
    }
}

function showLoader() {
    $('#loader').show();
    $('#overlay').show();
}

function hideLoader() {
    $('#loader').hide();
    $('#overlay').hide();
}

KioskBoard.run('.talon-keyboard', {
    /*!
    * Required
    * An Array of Objects has to be defined for the custom keys. Hint: Each object creates a row element (HTML) on the keyboard.
    * e.g. [{"key":"value"}, {"key":"value"}] => [{"0":"A","1":"B","2":"C"}, {"0":"D","1":"E","2":"F"}]
    */
    keysArrayOfObjects: null,
    /*!
    * Required only if "keysArrayOfObjects" is "null".
    * The path of the "kioskboard-keys-${langugage}.json" file must be set to the "keysJsonUrl" option. (XMLHttpRequest to get the keys from JSON file.)
    * e.g. '/Content/Plugins/KioskBoard/dist/kioskboard-keys-english.json'
    */
    keysJsonUrl: '/lib/kioskboard-keys-english.json',
    /*!
     * Optional: (Special Characters)* An Array of Strings can be set to override the built-in special characters.* 
     * e.g. ["#", "€", "%", "+", "-", "*"]
     */
    keysSpecialCharsArrayOfStrings: ["?", "!", "#", "€", "%", "+", "-", "*"],
    // Language Code (ISO 639-1) for custom keys (for language support) => e.g. "de" || "en" || "fr" || "hu" || "tr" etc...
    language: 'en',
    // Uppercase or lowercase to start. Uppercased when "true"
    capsLockActive: false,
    // Allow or prevent real/physical keyboard usage. Prevented when "false"// In addition, the "allowMobileKeyboard" option must be "true" as well, if the real/physical keyboard has wanted to be used.
    allowRealKeyboard: true
});

KioskBoard.run('.keyboard', {
    /*!
    * Required
    * An Array of Objects has to be defined for the custom keys. Hint: Each object creates a row element (HTML) on the keyboard.
    * e.g. [{"key":"value"}, {"key":"value"}] => [{"0":"A","1":"B","2":"C"}, {"0":"D","1":"E","2":"F"}]
    */
    keysArrayOfObjects: null,
    /*!
     * Optional: (Special Characters)* An Array of Strings can be set to override the built-in special characters.* 
     * e.g. ["#", "€", "%", "+", "-", "*"]
     */
    keysSpecialCharsArrayOfStrings: ["?", "!", "#", "€", "%", "+", "-", "*"],
    /*!
    * Required only if "keysArrayOfObjects" is "null".
    * The path of the "kioskboard-keys-${langugage}.json" file must be set to the "keysJsonUrl" option. (XMLHttpRequest to get the keys from JSON file.)
    * e.g. '/Content/Plugins/KioskBoard/dist/kioskboard-keys-english.json'
    */
    keysJsonUrl: '/lib/kioskboard-keys-russian.json',
    // Language Code (ISO 639-1) for custom keys (for language support) => e.g. "de" || "en" || "fr" || "hu" || "tr" etc...
    language: 'ru',
    // Uppercase or lowercase to start. Uppercased when "true"
    capsLockActive: false,
    // Allow or prevent real/physical keyboard usage. Prevented when "false"// In addition, the "allowMobileKeyboard" option must be "true" as well, if the real/physical keyboard has wanted to be used.
    allowRealKeyboard: true
});