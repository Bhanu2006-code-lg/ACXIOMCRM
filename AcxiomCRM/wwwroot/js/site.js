// Confirm delete dialogs
document.querySelectorAll('[data-confirm]').forEach(function (el) {
    el.addEventListener('click', function (e) {
        if (!confirm(this.dataset.confirm || 'Are you sure?')) e.preventDefault();
    });
});

// Auto-dismiss alerts after 4s
setTimeout(function () {
    document.querySelectorAll('.alert-dismissible').forEach(function (a) {
        var bsAlert = bootstrap.Alert.getOrCreateInstance(a);
        bsAlert.close();
    });
}, 4000);
