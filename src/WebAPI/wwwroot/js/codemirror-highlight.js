/* CodeMirror-based SQL syntax highlighting for static code blocks */
(function() {
    if (typeof window === 'undefined') return;

    var retryCount = 0;
    var maxRetries = 50; // 50 * 50ms = 2.5 seconds max

    function highlightAll() {
        // Wait for CodeMirror and SQL mode to be available
        if (!window.CodeMirror) {
            if (retryCount++ < maxRetries) {
                setTimeout(highlightAll, 50);
            } else {
                console.error('CodeMirror failed to load after ' + (maxRetries * 50) + 'ms');
            }
            return;
        }
        
        // Test if SQL mode is available and works correctly
        try {
            var testMode = CodeMirror.getMode({}, 'text/x-sql');
            if (!testMode || testMode.name === 'null') {
                if (retryCount++ < maxRetries) {
                    setTimeout(highlightAll, 50);
                } else {
                    console.error('CodeMirror SQL mode failed to load after ' + (maxRetries * 50) + 'ms');
                }
                return;
            }
            
            // Test if the mode actually works by trying to tokenize
            var testState = CodeMirror.startState(testMode);
            if (!testState) {
                if (retryCount++ < maxRetries) {
                    setTimeout(highlightAll, 50);
                } else {
                    console.error('CodeMirror startState failed');
                }
                return;
            }
            
            // Try a test tokenization
            var testStream = new CodeMirror.StringStream('SELECT');
            testMode.token(testStream, testState);
        } catch (e) {
            if (retryCount++ < maxRetries) {
                setTimeout(highlightAll, 50);
            } else {
                console.error('CodeMirror SQL mode validation error:', e);
            }
            return;
        }
        
        var elements = document.querySelectorAll('pre code.language-sql, code.language-sql');
        if (elements.length === 0) {
            return; // No elements to highlight
        }
        
        for (var i = 0; i < elements.length; i++) {
            try {
                highlightElement(elements[i]);
            } catch (e) {
                console.error('Failed to highlight element:', e);
            }
        }
    }

    function highlightElement(element) {
        try {
            var code = element.textContent;
            var highlighted = highlightSQL(code);
            element.innerHTML = highlighted;
            element.classList.add('cm-s-default');
        } catch (e) {
            console.error('highlightElement error:', e);
            throw e; // Re-throw so outer catch can handle it
        }
    }

    function highlightSQL(code) {
        if (!window.CodeMirror) {
            return escapeHtml(code);
        }
        
        try {
            var mode = CodeMirror.getMode({}, 'text/x-sql');
            var state = CodeMirror.startState(mode);
            var lines = code.split('\n');
            var html = [];
            
            for (var i = 0; i < lines.length; i++) {
                var line = lines[i];
                var stream = new CodeMirror.StringStream(line);
                var lineHtml = '';
                
                while (!stream.eol()) {
                    var style = mode.token(stream, state);
                    var text = escapeHtml(stream.current());
                    
                    if (style) {
                        lineHtml += '<span class="cm-' + style.replace(/ /g, ' cm-') + '">' + text + '</span>';
                    } else {
                        lineHtml += text;
                    }
                    
                    stream.start = stream.pos;
                }
                
                html.push(lineHtml || '\n');
            }
            
            return html.join('\n');
        } catch (e) {
            console.error('CodeMirror highlighting error:', e);
            return escapeHtml(code);
        }
    }

    function escapeHtml(str) {
        return str.replace(/&/g, '&amp;')
                  .replace(/</g, '&lt;')
                  .replace(/>/g, '&gt;')
                  .replace(/"/g, '&quot;')
                  .replace(/'/g, '&#039;');
    }

    // Run on page load
    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', highlightAll);
    } else {
        highlightAll();
    }

    // Expose for manual use
    window.CodeMirrorHighlight = {
        highlightAll: highlightAll,
        highlightElement: highlightElement
    };
})();
