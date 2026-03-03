/* Minimal Prism.js for SQL Syntax Highlighting */
(function() {
    if (typeof window === 'undefined') return;

    var Prism = {
        highlightAll: function() {
            var elements = document.querySelectorAll('pre code.language-sql, code.language-sql');
            for (var i = 0; i < elements.length; i++) {
                this.highlightElement(elements[i]);
            }
        },

        highlightElement: function(element) {
            var code = element.textContent;
            element.innerHTML = this.highlight(code);
        },

        highlight: function(code) {
            // SQL Keywords
            var keywords = /\b(SELECT|FROM|WHERE|AND|OR|NOT|IN|LIKE|BETWEEN|IS|NULL|AS|JOIN|LEFT|RIGHT|INNER|OUTER|ON|GROUP BY|ORDER BY|HAVING|LIMIT|OFFSET|UNION|ALL|DISTINCT|COUNT|SUM|AVG|MIN|MAX|CASE|WHEN|THEN|ELSE|END)\b/gi;
            
            // Comments
            var comments = /(--.*$)/gm;
            var blockComments = /(\/\*[\s\S]*?\*\/)/g;
            
            // Strings
            var strings = /('(?:[^'\\]|\\.)*')/g;
            
            // Numbers
            var numbers = /\b(\d+)\b/g;
            
            // Functions
            var functions = /\b([A-Za-z_]\w*)\s*(?=\()/g;

            // Store original to avoid double-escaping
            code = code.replace(/&/g, '&amp;')
                       .replace(/</g, '&lt;')
                       .replace(/>/g, '&gt;');

            // Apply highlighting
            code = code.replace(blockComments, '<span class="token comment">$1</span>');
            code = code.replace(comments, '<span class="token comment">$1</span>');
            code = code.replace(strings, '<span class="token string">$1</span>');
            code = code.replace(keywords, '<span class="token keyword">$1</span>');
            code = code.replace(numbers, '<span class="token number">$1</span>');
            code = code.replace(functions, '<span class="token function">$1</span>');

            return code;
        }
    };

    window.Prism = Prism;

    // Auto-run on page load
    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', function() {
            Prism.highlightAll();
        });
    } else {
        Prism.highlightAll();
    }
})();
