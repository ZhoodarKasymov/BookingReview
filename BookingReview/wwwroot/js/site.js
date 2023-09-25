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
    let pattern = /^(?:[A-Za-z]?\d+|\d+)$/;

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