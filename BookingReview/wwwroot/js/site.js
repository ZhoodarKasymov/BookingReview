// Please see documentation at https://docs.microsoft.com/aspnet/core/client-side/bundling-and-minification
// for details on configuring this project to bundle and minify static web assets.

// Write your JavaScript code.

function dangerAlert(message) {
    Swal.fire({
        html: `<div class="text-start">
                    <svg xmlns="http://www.w3.org/2000/svg" width="24" height="24" viewBox="0 0 24 24" fill="none">
                      <path d="M12 22C13.3135 22.0017 14.6143 21.7438 15.8278 21.2412C17.0413 20.7385 18.1435 20.001 19.071 19.071C20.001 18.1435 20.7385 17.0413 21.2412 15.8278C21.7438 14.6143 22.0017 13.3135 22 12C22.0017 10.6865 21.7438 9.3857 21.2411 8.17222C20.7385 6.95875 20.001 5.85656 19.071 4.92901C18.1435 3.99902 17.0413 3.26151 15.8278 2.75885C14.6143 2.25619 13.3135 1.99831 12 2.00001C10.6865 1.99833 9.3857 2.25623 8.17222 2.75889C6.95875 3.26154 5.85656 3.99904 4.92901 4.92901C3.99904 5.85656 3.26154 6.95875 2.75889 8.17222C2.25623 9.3857 1.99833 10.6865 2.00001 12C1.99831 13.3135 2.25619 14.6143 2.75885 15.8278C3.26151 17.0413 3.99902 18.1435 4.92901 19.071C5.85656 20.001 6.95875 20.7385 8.17222 21.2411C9.3857 21.7438 10.6865 22.0017 12 22Z" stroke="#DA1C1C" stroke-width="2" stroke-linejoin="round"/>
                      <path fill-rule="evenodd" clip-rule="evenodd" d="M12 18.5C12.3315 18.5 12.6495 18.3683 12.8839 18.1339C13.1183 17.8995 13.25 17.5815 13.25 17.25C13.25 16.9185 13.1183 16.6005 12.8839 16.3661C12.6495 16.1317 12.3315 16 12 16C11.6685 16 11.3505 16.1317 11.1161 16.3661C10.8817 16.6005 10.75 16.9185 10.75 17.25C10.75 17.5815 10.8817 17.8995 11.1161 18.1339C11.3505 18.3683 11.6685 18.5 12 18.5Z" fill="#DA1C1C"/>
                      <path d="M12 6V14" stroke="#DA1C1C" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"/>
                    </svg>
                    ${message}
              <div/>`,
        position: 'top',
        background: '#f8d7da',
        showConfirmButton: false,
        showCloseButton: true,
        customClass: {
            closeButton: 'my-close-button-class'
        },
        backdrop: false,
        timer: 3000
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